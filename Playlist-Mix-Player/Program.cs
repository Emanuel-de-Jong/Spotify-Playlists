using System.Diagnostics;

namespace Playlist_Mix_Player
{
    public enum EMixChoiceOption
    {
        Link,
        Playlist
    }

    public class Program
    {
        private const string DEFAULT_BATCH_FILE_NAME = "!!rnd_mix";

        private EMixChoiceOption mixChoiceOption = EMixChoiceOption.Link;
        private List<List<string>> linksByPlaylist = [];

        public static void Main(string[] args)
        {
            new Program().Run(args);
        }

        public void Run(string[] args)
        {
            string batchFileName = DEFAULT_BATCH_FILE_NAME;
            if (args.Length > 0)
            {
                batchFileName = args[0];
            }

            if (args.Length > 1)
            {
                if (int.TryParse(args[1], out int choiceInt) && Enum.IsDefined(typeof(EMixChoiceOption), choiceInt))
                {
                    mixChoiceOption = (EMixChoiceOption)choiceInt;
                }
                else if (!Enum.TryParse(args[1], true, out mixChoiceOption))
                {
                    throw new Exception("Invalid mix choice option. Use '0' for Link or '1' for Playlist.");
                }
            }

            ProcessBatchFile(batchFileName);

            if (args.Length > 0)
            {
                foreach (string playlistPath in GetPlaylistPaths(batchFileName))
                {
                    ProcessBatchFile(DEFAULT_BATCH_FILE_NAME, playlistPath);
                }
            }

            PlayRandomLink();
        }

        private void ProcessBatchFile(string batchFileName)
        {
            ProcessBatchFile(batchFileName, Environment.CurrentDirectory);
        }

        private void ProcessBatchFile(string batchFileName, string basePath)
        {
            string batchFilePath = $"{basePath}{Path.DirectorySeparatorChar}{batchFileName}.bat";
            if (!System.IO.File.Exists(batchFilePath))
            {
                throw new Exception($"{batchFilePath} not found.");
            }

            int linksId = linksByPlaylist.Count;
            foreach (string line in System.IO.File.ReadLines(batchFilePath))
            {
                if (line.Contains("http"))
                {
                    if (linksId == linksByPlaylist.Count)
                    {
                        linksByPlaylist.Add(new List<string>());
                    }

                    linksByPlaylist[linksId].Add(line.Trim());
                }
            }
        }

        private List<string> GetPlaylistPaths(string tag)
        {
            List<string> playlistPaths = [];

            string rootPath = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.FullName;
            foreach (string dirPath in Directory.GetDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
            {
                string dirName = Path.GetFileName(dirPath);
                if (dirName.StartsWith("!!") || dirName.StartsWith("zz"))
                {
                    continue;
                }

                foreach (string playlistPath in Directory.GetDirectories(dirPath, "*", SearchOption.TopDirectoryOnly))
                {
                    string? mp3Path = Directory.GetFiles(playlistPath, "*.mp3").FirstOrDefault();
                    if (mp3Path == null)
                    {
                        continue;
                    }

                    TagLib.File mp3File = TagLib.File.Create(mp3Path);
                    if (mp3File.Tag.Genres.Contains(tag))
                    {
                        playlistPaths.Add(playlistPath);
                    }
                }
            }

            return playlistPaths;
        }

        private void PlayRandomLink()
        {
            if (linksByPlaylist.Count == 0)
            {
                throw new Exception("No links in any batch file.");
            }

            Random random = new();
            List<string>? links = null;
            if (mixChoiceOption == EMixChoiceOption.Link)
            {
                links = linksByPlaylist.SelectMany(x => x).ToList();
            }
            else if (mixChoiceOption == EMixChoiceOption.Playlist)
            {
                links = linksByPlaylist[random.Next(linksByPlaylist.Count)];
            }

            if (links == null || links.Count == 0)
            {
                throw new Exception("No links.");
            }

            string selectedLink = links[random.Next(links.Count)];

            if (selectedLink.Contains("youtube.com"))
            {
                OpenYouTubeLink(selectedLink);
            }
            else if (selectedLink.Contains("spotify.com"))
            {
                OpenSpotifyLink(selectedLink);
            }
            else
            {
                throw new Exception("Unsupported link format. Use a Youtube video or Spotify playlist link.");
            }
        }

        private void OpenYouTubeLink(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private void OpenSpotifyLink(string url)
        {
            string playlistId = new Uri(url).Segments.Last().Split('?')[0];
            Process.Start(new ProcessStartInfo
            {
                FileName = $"spotify:playlist:{playlistId}",
                UseShellExecute = true
            });
        }
    }
}
