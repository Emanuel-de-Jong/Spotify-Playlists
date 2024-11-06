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
        private string batchFileName = "!!rnd_mix";
        private EMixChoiceOption mixChoiceOption = EMixChoiceOption.Link;
        private List<List<string>> linksByPlaylist = [];

        public static void Main(string[] args)
        {
            new Program().Run(args);
        }

        public void Run(string[] args)
        {
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
                    Console.WriteLine("Error: Invalid choice option. Use '0' for Link or '1' for Playlist, or the corresponding names.");
                    return;
                }
            }

            string batFilePath = $"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}{batchFileName}.bat";
            if (!File.Exists(batFilePath))
            {
                Console.WriteLine("Error: Batch file not found.");
                return;
            }

            List<string> playlistNames = ProcessBatchFile(batFilePath);
            if (playlistNames.Count > 0)
            {
                foreach (string playlistName in playlistNames)
                {
                    batFilePath = FindPlaylistBatFilePath(playlistName);
                    if (batFilePath != null)
                    {
                        ProcessBatchFile(batFilePath);
                    }
                }
            }

            PlayRandomLink();
        }

        private List<string> ProcessBatchFile(string filePath)
        {
            List<string> playlistNames = [];

            int linksId = linksByPlaylist.Count;

            foreach (string line in File.ReadLines(filePath))
            {
                string trimmedLine = line.Trim();
                if (trimmedLine == "GOTO PROGRAM_START")
                {
                    continue;
                }
                if (trimmedLine == ":PROGRAM_START")
                {
                    break;
                }

                if (trimmedLine.Contains("http"))
                {
                    if (linksId == linksByPlaylist.Count)
                    {
                        linksByPlaylist.Add(new List<string>());
                    }

                    linksByPlaylist[linksId].Add(trimmedLine);
                }
                else if (!string.IsNullOrEmpty(trimmedLine))
                {
                    playlistNames.Add(trimmedLine);
                }
            }

            return playlistNames;
        }

        private string? FindPlaylistBatFilePath(string playlistName)
        {
            foreach (string dirPath in Directory.GetDirectories(Directory.GetCurrentDirectory(), "*", SearchOption.AllDirectories))
            {
                string dirName = Path.GetFileName(dirPath);
                if (dirName.StartsWith("!!") || dirName.StartsWith("zz"))
                {
                    continue;
                }

                if (dirName.Equals(playlistName, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{dirPath}{Path.DirectorySeparatorChar}{batchFileName}.bat";
                }
            }

            return null;
        }

        private void PlayRandomLink()
        {
            if (linksByPlaylist.Count == 0)
            {
                Console.WriteLine("No links found in the batch file.");
                return;
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
                Console.WriteLine("No links found in the batch file.");
                return;
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
                Console.WriteLine("Error: Unsupported link format.");
            }
        }

        private void OpenYouTubeLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening YouTube link: " + ex.Message);
            }
        }

        private void OpenSpotifyLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "spotify",
                    Arguments = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening Spotify link: " + ex.Message);
            }
        }
    }
}
