using SpotifyAPI.Web;
using System.Xml;
using YamlDotNet.Serialization;

namespace Playlist_Merger
{
    public class Program
    {
        private IDeserializer yamlReader;
        private SpotifyClient spotifyClient;

        static void Main(string[] args)
        {
            new Program().Run().GetAwaiter().GetResult();
        }

        private async Task Run()
        {
            yamlReader = new DeserializerBuilder().Build();

            CreateSpotifyClient();

            Console.WriteLine("Done!");
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
    }
}
