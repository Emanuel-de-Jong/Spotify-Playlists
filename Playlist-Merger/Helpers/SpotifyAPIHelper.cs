using Playlist_Merger.Models;
using SpotifyAPI.Web;

namespace Playlist_Merger.Helpers
{
    public class SpotifyAPIHelper
    {
        private const int MAX_RETRIES = 3;

        public int RequestCount { get; set; }
        public string UserId { get; set; }

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
                    apiCredentials["RefreshToken"] = response.RefreshToken;
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
                    Scope = [Scopes.PlaylistModifyPrivate, Scopes.PlaylistReadPrivate, Scopes.PlaylistReadCollaborative]
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

        public async Task FetchPlaylistMetas(Playlists mixPlaylists, Playlists mergePlaylists)
        {
            PlaylistCurrentUsersRequest request = new() { Limit = 50 };
            List<FullPlaylist> responsePlaylists = await CallPaginated(
                () => spotifyClient.Playlists.CurrentUsers(request));
            foreach (FullPlaylist responsePlaylist in responsePlaylists)
            {
                if (responsePlaylist.Owner.Id != UserId)
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
                    FullPlaylist responsePlaylist = await Call(
                        () => spotifyClient.Playlists.Create(UserId, request));

                    mergePlaylists.Add(responsePlaylist);
                }
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

            PlaylistAddItemsRequest request = new(tracks.Select(t => "spotify:track:" + t).ToList());
            SnapshotResponse response = await Call(
                () => spotifyClient.Playlists.AddItems(playlist.Id, request));

            playlist.SnapshotId = response.SnapshotId;
        }

        private async Task RemoveTracksFromPlaylist(List<string> tracks, Playlist playlist)
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
