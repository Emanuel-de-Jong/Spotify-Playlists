using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Configuration;

namespace Add_Link_Info
{
    public class Program
    {
        private static string ApiKey;

        private YouTubeService? ytService;

        private static void Main(string[] args)
        {
            IConfigurationRoot appSettings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            ApiKey = appSettings["YoutubeApiKey"];

            new Program().Run().GetAwaiter().GetResult();
        }

        private async Task Run()
        {
            ytService = new YouTubeService(new BaseClientService.Initializer
            {
                ApplicationName = "Music-Playlist",
                ApiKey = ApiKey
            });

            Console.WriteLine("Starting to process batch files...");
            await ProcessBatchFiles();
            Console.WriteLine("Processing complete.");
        }

        private async Task ProcessBatchFiles()
        {
            string rootPath = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.FullName;
            foreach (string filePath in Directory.GetFiles(rootPath, "*.bat", SearchOption.AllDirectories))
            {
                string[] lines = await File.ReadAllLinesAsync(filePath);
                bool isFileModified = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Contains("youtube.com") || line.Contains("youtu.be") && !line.StartsWith('`'))
                    {
                        string? videoId = GetVideoId(line);
                        if (videoId == null)
                        {
                            continue;
                        }

                        Video? video = await GetYoutubeVideo(videoId);
                        if (video == null)
                        {
                            continue;
                        }

                        lines[i] = $"`[{video.Snippet.ChannelTitle}] {video.Snippet.Title}` {line}";
                        isFileModified = true;
                    }
                }

                if (isFileModified)
                {
                    await File.WriteAllLinesAsync(filePath, lines);
                    Console.WriteLine($"Updated file: {filePath}");
                }
            }
        }

        private string? GetVideoId(string url)
        {
            if (url.Contains("youtube.com"))
            {
                return url.Split("v=")[1].Split("&")[0];
            }

            if (url.Contains("youtu.be"))
            {
                return url.Split("/").Last().Split("?")[0];
            }

            return null;
        }

        private async Task<Video?> GetYoutubeVideo(string videoId)
        {
            VideosResource.ListRequest request = ytService.Videos.List("snippet");
            request.Id = videoId;

            VideoListResponse response = await request.ExecuteAsync();
            if (response.Items.Count > 0)
            {
                return response.Items[0];
            }

            return null;
        }
    }
}
