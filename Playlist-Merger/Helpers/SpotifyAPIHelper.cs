using Playlist_Merger.Models;
using SpotifyAPI.Web;

namespace Playlist_Merger.Helpers
{
    public class SpotifyAPIHelper
    {
        private const int MAX_RETRIES = 1;

        public int RequestCount { get; set; }

        private SpotifyClient spotifyClient;

        public async Task<SpotifyClient> CreateSpotifyClient(Dictionary<string, string> apiCredentials)
        {
            string clientId = apiCredentials["ClientId"];
            string clientSecret = apiCredentials["ClientSecret"];
            Uri redirectUri = new(apiCredentials["RedirectUri"]);
            string? refreshToken = apiCredentials.TryGetValue("RefreshToken", out string? value) ? value : null;
            string? accessToken = null;

            if (refreshToken != null)
            {
                try
                {
                    AuthorizationCodeRefreshResponse response = await new OAuthClient().RequestToken(
                        new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken));

                    accessToken = response.AccessToken;
                }
                catch (APIUnauthorizedException)
                {
                    Console.WriteLine("Refresh token is invalid. Re-authentication is required.");
                }
            }

            if (accessToken == null)
            {
                LoginRequest loginRequest = new(
                    redirectUri,
                    clientId,
                    LoginRequest.ResponseType.Code)
                {
                    Scope = [Scopes.PlaylistReadPrivate, Scopes.PlaylistModifyPrivate]
                };

                Uri uri = loginRequest.ToUri();
                Console.WriteLine($"Login and paste the URI here: {uri}");
                string responseUri = Console.ReadLine();
                string code = responseUri.Split("code=")[1].Trim();

                AuthorizationCodeTokenResponse response = await new OAuthClient().RequestToken(
                    new AuthorizationCodeTokenRequest(clientId, clientSecret, code, redirectUri));

                accessToken = response.AccessToken;
                apiCredentials["RefreshToken"] = response.RefreshToken;
            }

            spotifyClient = new(accessToken);
            return spotifyClient;
        }

        public async Task<T> Call<T>(Func<Task<T>> requestFunc)
        {
            return await ExecuteWithRetries(async () => await requestFunc());
        }

        public async Task<List<T>> CallPaginated<T>(Func<Task<Paging<T>>> requestFunc)
        {
            List<T> allItems = [];

            Paging<T> response = null;
            await ExecuteWithRetries(async () => response = await requestFunc());
            while (response != null)
            {
                allItems.AddRange(response.Items);
                if (response.Next == null)
                {
                    break;
                }
                await ExecuteWithRetries(async () => response = await spotifyClient.NextPage(response));
            }

            return allItems;
        }

        private async Task<TResult> ExecuteWithRetries<TResult>(Func<Task<TResult>> func)
        {
            int retryCount = 0;
            while (retryCount <= MAX_RETRIES)
            {
                try
                {
                    RequestCount++;
                    return await func();
                }
                catch (APITooManyRequestsException rateLimitEx)
                {
                    if (rateLimitEx.Response.Headers.TryGetValue("Retry-After", out string retryAfterStr) &&
                        int.TryParse(retryAfterStr, out int retryAfter))
                    {
                        retryAfter++;
                        Console.WriteLine($"Rate limit hit. Retrying after {retryAfter} seconds...");
                        await Task.Delay(retryAfter * 1000);

                        retryCount++;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            throw new Exception("Max retry attempts reached. Could not complete the request.");
        }

        public async Task AddTracksToPlaylist(List<string> tracks, Playlist playlist)
        {
            playlist.Tracks.AddRange(tracks);

            PlaylistAddItemsRequest request = new(tracks.Select(t => "spotify:track:" + t).ToList());
            SnapshotResponse response = await Call(
                () => spotifyClient.Playlists.AddItems(playlist.Id, request));

            playlist.SnapshotId = response.SnapshotId;
        }

        public async Task RemoveTracksFromPlaylist(List<string> tracks, Playlist playlist)
        {
            playlist.Tracks = playlist.Tracks.Except(tracks).ToList();

            PlaylistRemoveItemsRequest request = new()
            {
                Tracks = tracks.Select(t => new PlaylistRemoveItemsRequest.Item()
                {
                    Uri = "spotify:track:" + t
                }).ToList()
            };
            SnapshotResponse response = await Call(
                () => spotifyClient.Playlists.RemoveItems(playlist.Id, request));

            playlist.SnapshotId = response.SnapshotId;
        }
    }
}
