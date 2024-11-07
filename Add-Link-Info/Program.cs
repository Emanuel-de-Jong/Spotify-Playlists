using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace Add_Link_Info
{
    public class Program
    {
        private static readonly string BaseDirectory = Path.Combine("..", "..", "KBOT's-Mixes");
        private static string ApiKey;

        private static void Main(string[] args)
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            ApiKey = config["YoutubeApiKey"];

            new Program().Run(args).GetAwaiter().GetResult();
        }

        private async Task Run(string[] args)
        {
            Console.WriteLine("Starting to process batch files...");
            await ProcessBatchFiles(BaseDirectory);
            Console.WriteLine("Processing complete.");
        }

        private async Task ProcessBatchFiles(string directory)
        {
            foreach (string file in Directory.GetFiles(directory, "*.bat", SearchOption.AllDirectories))
            {
                string[] lines = await File.ReadAllLinesAsync(file);
                bool fileModified = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (IsYoutubeLink(line) && !HasInfo(line))
                    {
                        Video video = await GetYoutubeVideo(GetYoutubeId(line));
                        if (video != null)
                        {
                            lines[i] = $"`[{video.Snippet.ChannelTitle}] {video.Snippet.Title}` {line}";
                            fileModified = true;
                        }
                    }
                }

                if (fileModified)
                {
                    await File.WriteAllLinesAsync(file, lines);
                    Console.WriteLine($"Updated file: {file}");
                }
            }
        }

        private bool IsYoutubeLink(string line)
        {
            return line.Contains("youtube.com") || line.Contains("youtu.be");
        }

        private bool HasInfo(string line)
        {
            return line.StartsWith("`");
        }

        private string GetYoutubeId(string url)
        {
            Match match = Regex.Match(url, @"(?:youtu\.be/|youtube\.com.*(?:v=|embed/|v/|shorts/|watch\?v=))([^"]{ 11})");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private async Task<Video> GetYoutubeVideo(string videoId)
        {
            if (string.IsNullOrEmpty(videoId))
            {
                return null;
            }

            try
            {
                YouTubeService youtubeService = new(new BaseClientService.Initializer
                {
                    ApiKey = ApiKey,
                    ApplicationName = this.GetType().ToString()
                });

                VideosResource.ListRequest request = youtubeService.Videos.List("snippet");
                request.Id = videoId;

                VideoListResponse response = await request.ExecuteAsync();

                if (response.Items.Count > 0)
                {
                    return response.Items[0];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching data for video ID {videoId}: {ex.Message}");
            }

            return null;
        }
    }
}
