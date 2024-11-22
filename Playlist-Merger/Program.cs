using Playlist_Merger.Helpers;
using Playlist_Merger.Models;
using SpotifyAPI.Web;
using YamlDotNet.Serialization;

namespace Playlist_Merger
{
    public class Program
    {
        private ISerializer serializer;
        private IDeserializer deserializer;

        private SpotifyAPIHelper spotifyAPIHelper = new();
        private SpotifyClient spotifyClient;
        private string userId;

        private Playlists oldMixPlaylists = [];
        private Playlists oldMergePlaylists = [];
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

            Console.WriteLine("Creating Spotify client");
            await CreateSpotifyClient();

            Console.WriteLine("Getting user id");
            await GetUserId();

            Console.WriteLine("Fetching playlist metas");
            await spotifyAPIHelper.FetchPlaylistMetas(mixPlaylists, mergePlaylists);

            Console.WriteLine("Creating missing merge playlists");
            await spotifyAPIHelper.CreateMergePlaylists(mergePlaylists);

            Console.WriteLine("Getting mix playlist tracks");
            await GetPlaylistTracks(mixPlaylists, oldMixPlaylists);
            SavePlaylists(mixPlaylists, "Mix-Playlists");

            Console.WriteLine("Getting merge playlist tracks");
            await GetPlaylistTracks(mergePlaylists, oldMergePlaylists);
            SavePlaylists(mergePlaylists, "Merge-Playlists");

            Console.WriteLine("Updating merge playlists");
            await spotifyAPIHelper.UpdateMergePlaylists(mixPlaylists, mergePlaylists);
            SavePlaylists(mergePlaylists, "Merge-Playlists");

            Console.WriteLine($"Request count: {spotifyAPIHelper.RequestCount}");
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

            spotifyClient = await spotifyAPIHelper.CreateSpotifyClient(apiCredentials);

            yamlContent = serializer.Serialize(apiCredentials);
            File.WriteAllText("Spotify-API.yaml", yamlContent);
        }

        private async Task GetUserId()
        {
            if (File.Exists("Cache.yaml"))
            {
                string yamlContent = File.ReadAllText("Cache.yaml");
                userId = deserializer.Deserialize<string>(yamlContent);
            }
            else
            {
                userId = (await spotifyAPIHelper.Call(
                    () => spotifyClient.UserProfile.Current())).Id;

                string yamlContent = serializer.Serialize(userId);
                File.WriteAllText($"Cache.yaml", yamlContent);
            }

            spotifyAPIHelper.UserId = userId;
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

        private void SavePlaylists(Playlists playlists, string filename)
        {
            string yamlContent = serializer.Serialize(playlists);
            File.WriteAllText($"{filename}.yaml", yamlContent);
        }
    }
}
