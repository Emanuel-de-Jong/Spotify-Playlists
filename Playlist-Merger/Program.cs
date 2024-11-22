using Playlist_Merger.Helpers;
using Playlist_Merger.Models;
using SpotifyAPI.Web;

namespace Playlist_Merger
{
    public class Program
    {
        private YAMLHelper yamlHelper = new();
        private SpotifyAPIHelper spotifyAPIHelper = new();

        private Dictionary<string, string> cache;
        private Dictionary<string, string> apiCredentials;
        private SpotifyClient spotifyClient;
        private string? userId;

        private Playlists oldMixPlaylists;
        private Playlists oldMergePlaylists;
        private Playlists mixPlaylists = [];
        private Playlists mergePlaylists;

        private static void Main(string[] args)
        {
            new Program().Run().GetAwaiter().GetResult();
        }

        private async Task Run()
        {
            try
            {
                Console.WriteLine("Loading YAMLs");
                LoadYAMLs();

                Console.WriteLine("Creating Spotify client");
                await CreateSpotifyClient();

                Console.WriteLine("Getting user id");
                await GetUserId();

                Console.WriteLine("Fetching playlist metas");
                await FetchPlaylistMetas(mixPlaylists, mergePlaylists);

                Console.WriteLine("Creating missing merge playlists");
                await CreateMergePlaylists(mergePlaylists);

                Console.WriteLine("Getting mix playlist tracks");
                await GetPlaylistTracks(mixPlaylists, oldMixPlaylists);

                Console.WriteLine("Getting merge playlist tracks");
                await GetPlaylistTracks(mergePlaylists, oldMergePlaylists);

                Console.WriteLine("Updating merge playlists");
                await UpdateMergePlaylists(mixPlaylists, mergePlaylists);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("Saving changes locally");
            yamlHelper.SaveAll(cache, apiCredentials, mixPlaylists, mergePlaylists);

            Console.WriteLine($"Request count: {spotifyAPIHelper.RequestCount}");
            Console.WriteLine("Done!");
        }

        private void LoadYAMLs()
        {
            cache = yamlHelper.LoadCache();
            mergePlaylists = yamlHelper.LoadMergePlaylistsDeps();

            oldMixPlaylists = yamlHelper.Deserialize<Playlists>(YAMLHelper.MIX_FILE_NAME);
            oldMixPlaylists ??= [];

            oldMergePlaylists = yamlHelper.Deserialize<Playlists>(YAMLHelper.MERGE_FILE_NAME);
            oldMergePlaylists ??= [];
        }

        private async Task CreateSpotifyClient()
        {
            apiCredentials = yamlHelper.Deserialize<Dictionary<string, string>>(YAMLHelper.SPOTIFY_API_FILE_NAME);
            spotifyClient = await spotifyAPIHelper.CreateSpotifyClient(apiCredentials);
        }

        private async Task GetUserId()
        {
            if (!cache.TryGetValue("UserId", out userId))
            {
                userId = (await spotifyAPIHelper.Call(
                    () => spotifyClient.UserProfile.Current())).Id;
                cache["UserId"] = userId;
            }
        }

        public async Task FetchPlaylistMetas(Playlists mixPlaylists, Playlists mergePlaylists)
        {
            PlaylistCurrentUsersRequest request = new() { Limit = 50 };
            List<FullPlaylist> responsePlaylists = await spotifyAPIHelper.CallPaginated(
                () => spotifyClient.Playlists.CurrentUsers(request));
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

        public async Task CreateMergePlaylists(Playlists mergePlaylists)
        {
            foreach (KeyValuePair<string, Playlist> pair in mergePlaylists)
            {
                if (pair.Value.Id == null)
                {
                    PlaylistCreateRequest request = new(pair.Key) { Public = false };
                    FullPlaylist responsePlaylist = await spotifyAPIHelper.Call(
                        () => spotifyClient.Playlists.Create(userId, request));

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

                PlaylistGetItemsRequest request = new() { Limit = 100 };
                List<PlaylistTrack<IPlayableItem>> items = await spotifyAPIHelper.CallPaginated(
                    () => spotifyClient.Playlists.GetItems(playlist.Id, request));

                playlist.Tracks = items
                    .Select(i => i.Track)
                    .OfType<FullTrack>()
                    .Select(track => track.Id)
                    .ToList();
            }
        }

        public async Task UpdateMergePlaylists(Playlists mixPlaylists, Playlists mergePlaylists)
        {
            foreach (Playlist mergePlaylist in mergePlaylists.Values)
            {
                List<string>? newTracks = null;
                if (mergePlaylist.IsInclusive)
                {
                    newTracks = mergePlaylist.Deps
                        .SelectMany(dep => mixPlaylists[dep].Tracks)
                        .ToList();
                }
                else
                {
                    newTracks = mixPlaylists
                        .Where(p => !mergePlaylist.Deps.Contains(p.Key))
                        .SelectMany(p => p.Value.Tracks)
                        .ToList();
                }

                List<string> addedTracks = newTracks.Except(mergePlaylist.Tracks).ToList();
                for (int i = 0; i < addedTracks.Count; i += 100)
                {
                    List<string> batch = addedTracks.Skip(i).Take(100).ToList();
                    await spotifyAPIHelper.AddTracksToPlaylist(batch, mergePlaylist);
                }

                List<string> removedTracks = mergePlaylist.Tracks.Except(newTracks).ToList();
                for (int i = 0; i < removedTracks.Count; i += 100)
                {
                    List<string> batch = removedTracks.Skip(i).Take(100).ToList();
                    await spotifyAPIHelper.RemoveTracksFromPlaylist(batch, mergePlaylist);
                }
            }
        }
    }
}
