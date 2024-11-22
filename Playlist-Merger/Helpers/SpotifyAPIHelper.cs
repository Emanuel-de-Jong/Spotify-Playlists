using SpotifyAPI.Web;

namespace Playlist_Merger.Helpers
{
    public class SpotifyAPIHelper
    {
        private const int MAX_RETRIES = 3;

        public static async Task<T> Call<T>(Func<Task<T>> requestFunc)
        {
            return await ExecuteWithRetries(async () => await requestFunc());
        }

        public static async Task<List<T>> CallPaginated<T>(Func<Task<Paging<T>>> requestFunc, SpotifyClient spotifyClient)
        {
            return await ExecuteWithRetries(async () =>
            {
                List<T> allItems = [];
                Paging<T> response = await requestFunc();
                while (response != null)
                {
                    allItems.AddRange(response.Items);
                    if (response.Next == null)
                    {
                        break;
                    }
                    response = await spotifyClient.NextPage(response);
                }

                return allItems;
            });
        }

        private static async Task<TResult> ExecuteWithRetries<TResult>(Func<Task<TResult>> func)
        {
            int retryCount = 0;
            while (retryCount <= MAX_RETRIES)
            {
                try
                {
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
    }
}
