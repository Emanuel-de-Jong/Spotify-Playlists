using SpotifyAPI.Web;

namespace Playlist_Merger.Helpers
{
    public class SpotifyAPIHelper
    {
        private const int MaxRetries = 3;

        public async Task<List<T>> FetchSpotifyData<T>(Func<Task<Paging<T>>> requestFunction, SpotifyClient spotifyClient)
        {
            List<T> allItems = [];
            int retryCount = 0;

            while (retryCount <= MaxRetries)
            {
                try
                {
                    Paging<T> response = await requestFunction();

                    while (response != null)
                    {
                        allItems.AddRange(response.Items);

                        if (response.Next == null)
                        {
                            break;
                        }

                        response = await spotifyClient.NextPage(response);
                    }
                }
                catch (APIException apiEx)
                {
                    if (apiEx.Response != null && apiEx.Response.Headers.TryGetValue("Retry-After", out string value))
                    {
                        int retryAfterSeconds = int.Parse(value);
                        Console.WriteLine($"Rate limit hit. Retrying after {retryAfterSeconds} seconds...");
                        await Task.Delay(retryAfterSeconds * 1000);
                        retryCount++;
                    }
                    else
                    {
                        throw;
                    }
                }

                if (retryCount >= MaxRetries)
                {
                    throw new Exception("Max retry attempts reached. Could not complete the request.");
                }
            }

            return allItems;
        }
    }
}
