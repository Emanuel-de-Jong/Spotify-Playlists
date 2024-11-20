using Playlist_Merger.Classes;
using SpotifyAPI.Web;
using YamlDotNet.Serialization;

namespace Playlist_Merger
{
    public class Program
    {
        private IDeserializer yamlReader;
        private MergePlaylistsDeps mergePlaylistDeps;
        private Playlists oldMixPlaylists = [];
        private Playlists oldMergePlaylists = [];
        private SpotifyClient spotifyClient;
        private Playlists playlistMetas;
        private Playlists mixPlaylists;
        private Playlists mergePlaylists;

        static void Main(string[] args)
        {
            new Program().Run().GetAwaiter().GetResult();
        }

        private async Task Run()
        {
            yamlReader = new DeserializerBuilder().Build();

            LoadMergePlaylistsDeps();
            LoadOldPlaylists();
            CreateSpotifyClient();

            Console.WriteLine("Done!");
        }

        private void LoadMergePlaylistsDeps()
        {
            string yamlContent = File.ReadAllText("Merge-Playlists-Deps.yaml");
            mergePlaylistDeps = yamlReader.Deserialize<MergePlaylistsDeps>(yamlContent);
        }

        private void LoadOldPlaylists()
        {
            if (File.Exists("Mix-Playlists.yaml"))
            {
                string yamlContent = File.ReadAllText("Mix-Playlists.yaml");
                oldMixPlaylists = yamlReader.Deserialize<Playlists>(yamlContent);
            }

            if (File.Exists("Merge-Playlists.yaml"))
            {
                string yamlContent = File.ReadAllText("Merge-Playlists.yaml");
                oldMergePlaylists = yamlReader.Deserialize<Playlists>(yamlContent);
            }
        }

        private void CreateSpotifyClient()
        {
            string yamlContent = File.ReadAllText("Spotify-API.yaml");
            Dictionary<string, string> apiCredentials = yamlReader.Deserialize<Dictionary<string, string>>(yamlContent);
            SpotifyClientConfig spotifyClientConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new ClientCredentialsAuthenticator(
                    apiCredentials["SpotifyClientId"],
                    apiCredentials["SpotifyClientSecret"]));
            spotifyClient = new SpotifyClient(spotifyClientConfig);
        }

        private async Task FetchPlaylistMetas()
        {
        }
    }
}
