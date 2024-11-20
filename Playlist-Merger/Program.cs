using Playlist_Merger.Classes;
using SpotifyAPI.Web;
using YamlDotNet.Serialization;

namespace Playlist_Merger
{
    public class Program
    {
        private ISerializer serializer;
        private IDeserializer deserializer;
        private MergePlaylistsDeps mergePlaylistDeps;
        private Playlists oldMixPlaylists = [];
        private Playlists oldMergePlaylists = [];
        private SpotifyClient spotifyClient;
        private Playlists playlistMetas;
        private Playlists mixPlaylists;
        private Playlists mergePlaylists;

        private static void Main(string[] args)
        {
            new Program().Run().GetAwaiter().GetResult();
        }

        private async Task Run()
        {
            serializer = new SerializerBuilder().Build();
            deserializer = new DeserializerBuilder().Build();

            LoadMergePlaylistsDeps();
            LoadOldPlaylists();
            await CreateSpotifyClient();

            await FetchPlaylistMetas();

            Console.WriteLine("Done!");
        }

        private void LoadMergePlaylistsDeps()
        {
            string yamlContent = File.ReadAllText("Merge-Playlists-Deps.yaml");
            mergePlaylistDeps = deserializer.Deserialize<MergePlaylistsDeps>(yamlContent);
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
            string? refreshToken = apiCredentials.ContainsKey("RefreshToken") ? apiCredentials["RefreshToken"] : null;
            string? accessToken = null;

            if (refreshToken != null)
            {
                AuthorizationCodeRefreshResponse tokenResponse = await new OAuthClient().RequestToken(
                    new AuthorizationCodeRefreshRequest(
                        clientId,
                        clientSecret,
                        refreshToken
                    )
                );

                if (!tokenResponse.IsExpired)
                {
                    accessToken = tokenResponse.AccessToken;
                    apiCredentials["RefreshToken"] = tokenResponse.RefreshToken;
                }
            }

            if (accessToken == null)
            {
                LoginRequest loginRequest = new(
                    redirectUri,
                    clientId,
                    LoginRequest.ResponseType.Code)
                {
                    Scope = new[] { Scopes.PlaylistReadPrivate }
                };

                Uri uri = loginRequest.ToUri();
                Console.WriteLine($"Login in and paste the uri here {uri}");
                string responseUri = Console.ReadLine();
                string code = responseUri.Split("code=")[1].Trim();

                AuthorizationCodeTokenResponse tokenResponse = await new OAuthClient().RequestToken(
                    new AuthorizationCodeTokenRequest(
                        clientId,
                        clientSecret,
                        code,
                        redirectUri
                    )
                );

                accessToken = tokenResponse.AccessToken;
                apiCredentials["RefreshToken"] = tokenResponse.RefreshToken;
            }

            spotifyClient = new SpotifyClient(accessToken);

            yamlContent = serializer.Serialize(apiCredentials);
            File.WriteAllText("Spotify-API.yaml", yamlContent);
        }

        private async Task FetchPlaylistMetas()
        {
            Paging<FullPlaylist> playlists = await spotifyClient.Playlists.CurrentUsers();
            foreach (FullPlaylist playlist in playlists.Items)
            {
                Console.WriteLine($"Playlist Name: {playlist.Name}, Is Public: {playlist.Public}");
            }
        }
    }
}
