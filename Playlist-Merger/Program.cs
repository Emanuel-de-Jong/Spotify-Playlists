using Playlist_Merger.Models;
using SpotifyAPI.Web;
using YamlDotNet.Serialization;

namespace Playlist_Merger
{
    public class Program
    {
        private ISerializer serializer;
        private IDeserializer deserializer;
        private Playlists oldMixPlaylists = [];
        private Playlists oldMergePlaylists = [];
        private SpotifyClient spotifyClient;
        private string userId;
        private Playlists mixPlaylists = [];
        private Playlists mergePlaylists;

        private static void Main(string[] args)
        {
            new Program().Run().GetAwaiter().GetResult();
        }

        private async Task Run()
        {
            serializer = new SerializerBuilder().Build();
            deserializer = new DeserializerBuilder().Build();

            Console.WriteLine("Loading YAMLs");
            LoadMergePlaylistsDeps();
            LoadOldPlaylists();

            Console.WriteLine("Creating spotify client");
            await CreateSpotifyClient();

            userId = (await spotifyClient.UserProfile.Current()).Id;

            Console.WriteLine("fetching playlist metas");
            await FetchPlaylistMetas();
            Console.WriteLine("Creating missing merge playlists");
            await CreateMergePlaylists();

            Console.WriteLine("Getting mix playlist tracks");
            await GetPlaylistTracks(mixPlaylists, oldMixPlaylists);
            SavePlaylists(mixPlaylists, "Mix-Playlists");

            Console.WriteLine("Getting merge playlist tracks");
            await GetPlaylistTracks(mergePlaylists, oldMergePlaylists);
            SavePlaylists(mergePlaylists, "Merge-Playlists");

            Console.WriteLine("Updating merge playlists");
            await UpdateMergePlaylists();
            SavePlaylists(mergePlaylists, "Merge-Playlists");

            Console.WriteLine("Done!");
        }

        private void LoadMergePlaylistsDeps()
        {
            string yamlContent = File.ReadAllText("Merge-Playlists-Deps.yaml");
            mergePlaylists = deserializer.Deserialize<Playlists>(yamlContent);
            foreach (Playlist mergePlaylist in mergePlaylists.Values)
            {
                mergePlaylist.Deps = mergePlaylist.Deps.Select(d => $"KBOT's {d} Mix").ToList();
            }
        }

        private void LoadOldPlaylists()
        {
            if (File.Exists("Mix-Playlists.yaml"))
            {
                string yamlContent = File.ReadAllText("Mix-Playlists.yaml");
                oldMixPlaylists = deserializer.Deserialize<Playlists>(yamlContent);
            }

            if (File.Exists("Merge-Playlists.yaml"))
            {
                string yamlContent = File.ReadAllText("Merge-Playlists.yaml");
                oldMergePlaylists = deserializer.Deserialize<Playlists>(yamlContent);
            }
        }

        private async Task CreateSpotifyClient()
        {
            string yamlContent = File.ReadAllText("Spotify-API.yaml");
            Dictionary<string, string> apiCredentials = deserializer.Deserialize<Dictionary<string, string>>(yamlContent);
            string clientId = apiCredentials["ClientId"];
            string clientSecret = apiCredentials["ClientSecret"];
            Uri redirectUri = new(apiCredentials["RedirectUri"]);
            string? refreshToken = apiCredentials.TryGetValue("RefreshToken", out string? value) ? value : null;
            string? accessToken = null;

            if (refreshToken != null)
            {
                AuthorizationCodeRefreshResponse response = await new OAuthClient().RequestToken(
                    new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken));

                if (!response.IsExpired)
                {
                    accessToken = response.AccessToken;
                    apiCredentials["RefreshToken"] = response.RefreshToken;
                }
            }

            if (accessToken == null)
            {
                LoginRequest loginRequest = new(
                    redirectUri,
                    clientId,
                    LoginRequest.ResponseType.Code)
                {
                    Scope = [Scopes.PlaylistModifyPrivate]
                };

                Uri uri = loginRequest.ToUri();
                Console.WriteLine($"Login in and paste the uri here {uri}");
                string responseUri = Console.ReadLine();
                string code = responseUri.Split("code=")[1].Trim();

                AuthorizationCodeTokenResponse response = await new OAuthClient().RequestToken(
                    new AuthorizationCodeTokenRequest(clientId, clientSecret, code, redirectUri));

                accessToken = response.AccessToken;
                apiCredentials["RefreshToken"] = response.RefreshToken;
            }

            spotifyClient = new SpotifyClient(accessToken);

            yamlContent = serializer.Serialize(apiCredentials);
            File.WriteAllText("Spotify-API.yaml", yamlContent);
        }

        private async Task FetchPlaylistMetas()
        {
            PlaylistCurrentUsersRequest request = new() { Limit = 50 };
            Paging<FullPlaylist> response = await spotifyClient.Playlists.CurrentUsers(request);

            List<FullPlaylist> responsePlaylists = [];
            while (response != null)
            {
                responsePlaylists.AddRange(response.Items);
                if (response.Next == null)
                {
                    break;
                }
                response = await spotifyClient.NextPage(response);
            }

            foreach (FullPlaylist responsePlaylist in responsePlaylists)
            {
                if (responsePlaylist.Owner.Id != userId)
                {
                    continue;
                }

                if (responsePlaylist.Name.StartsWith("KBOT"))
                {
                    mixPlaylists.Add(responsePlaylist);
                }
                else if (mergePlaylists.ContainsKey(responsePlaylist.Name))
                {
                    mergePlaylists.Add(responsePlaylist);
                }
            }
        }

        private async Task CreateMergePlaylists()
        {
            foreach (KeyValuePair<string, Playlist> pair in mergePlaylists)
            {
                if (pair.Value.Id == null)
                {
                    PlaylistCreateRequest request = new(pair.Key) { Public = false };
                    FullPlaylist responsePlaylist = await spotifyClient.Playlists.Create(userId, request);

                    mergePlaylists.Add(responsePlaylist);
                }
            }
        }

        private async Task GetPlaylistTracks(Playlists playlists, Playlists oldPlaylists)
        {
            foreach (string playlistName in playlists.Keys)
            {
                Playlist playlist = playlists[playlistName];
                oldPlaylists.TryGetValue(playlistName, out Playlist? oldPlaylist);
                if (oldPlaylist != null &&
                    oldPlaylist.Id == playlist.Id &&
                    oldPlaylist.SnapshotId == playlist.SnapshotId)
                {
                    playlist.Tracks = oldPlaylist.Tracks;
                    continue;
                }

                PlaylistGetItemsRequest request = new() { Limit = 50 };
                Paging<PlaylistTrack<IPlayableItem>> response = await spotifyClient.Playlists.GetItems(playlist.Id, request);
                List<PlaylistTrack<IPlayableItem>> items = [];
                while (response != null)
                {
                    items.AddRange(response.Items);
                    if (response.Next == null)
                    {
                        break;
                    }
                    response = await spotifyClient.NextPage(response);
                }

                playlist.Tracks = items
                    .Select(i => i.Track)
                    .OfType<FullTrack>()
                    .Select(track => track.Id)
                    .ToList();
            }
        }

        private void SavePlaylists(Playlists playlists, string filename)
        {
            string yamlContent = serializer.Serialize(playlists);
            File.WriteAllText($"{filename}.yaml", yamlContent);
        }

        private async Task UpdateMergePlaylists()
        {
            foreach (Playlist mergePlaylist in mergePlaylists.Values)
            {
                List<string> newTracks = mergePlaylist.Deps
                    .SelectMany(dep => mixPlaylists[dep].Tracks)
                    .ToList();

                List<string> addedTracks = newTracks.Except(mergePlaylist.Tracks).ToList();
                for (int i = 0; i < addedTracks.Count; i += 100)
                {
                    List<string> batch = addedTracks.Skip(i).Take(100).ToList();
                    await AddTracksToPlaylist(batch, mergePlaylist);
                }

                List<string> removedTracks = mergePlaylist.Tracks.Except(newTracks).ToList();
                for (int i = 0; i < removedTracks.Count; i += 100)
                {
                    List<string> batch = removedTracks.Skip(i).Take(100).ToList();
                    await RemoveTracksFromPlaylist(batch, mergePlaylist);
                }
            }
        }

        private async Task AddTracksToPlaylist(List<string> tracks, Playlist playlist)
        {
            playlist.Tracks.AddRange(tracks);

            PlaylistAddItemsRequest addRequest = new(tracks.Select(t => "spotify:track:" + t).ToList());
            SnapshotResponse response = await spotifyClient.Playlists.AddItems(playlist.Id, addRequest);

            playlist.SnapshotId = response.SnapshotId;
        }

        private async Task RemoveTracksFromPlaylist(List<string> tracks, Playlist playlist)
        {
            playlist.Tracks = playlist.Tracks.Except(tracks).ToList();

            PlaylistRemoveItemsRequest removeRequest = new()
            {
                Tracks = tracks.Select(t => new PlaylistRemoveItemsRequest.Item()
                {
                    Uri = "spotify:track:" + t
                }).ToList()
            };
            SnapshotResponse response = await spotifyClient.Playlists.RemoveItems(playlist.Id, removeRequest);

            playlist.SnapshotId = response.SnapshotId;
        }
    }
}
