using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Playlist_Mix_Player
{
    internal class Program
    {
        public enum EMixChoiceOption
        {
            Link,
            Playlist
        }

        private static readonly Regex LinkRegex = new(@"http", RegexOptions.Compiled);

        public static void Main(string[] args)
        {
            EMixChoiceOption choiceOption = EMixChoiceOption.Link;

            if (args.Length >= 1)
            {
                if (int.TryParse(args[0], out int choiceInt) && Enum.IsDefined(typeof(EMixChoiceOption), choiceInt))
                {
                    choiceOption = (EMixChoiceOption)choiceInt;
                }
                else if (!Enum.TryParse(args[0], true, out choiceOption))
                {
                    Console.WriteLine("Error: Invalid choice option. Use '0' for Link or '1' for Playlist, or the corresponding names.");
                    return;
                }
            }

            string batFilePath = FindBatchFilePath();
            if (string.IsNullOrEmpty(batFilePath) || !File.Exists(batFilePath))
            {
                Console.WriteLine("Error: Batch file not found in the current directory.");
                return;
            }

            List<string> links = new();
            List<string> playlists = new();
            ProcessBatchFile(batFilePath, links, playlists);

            if (choiceOption == EMixChoiceOption.Link)
            {
                SelectRandomLink(links);
            }
            else if (choiceOption == EMixChoiceOption.Playlist)
            {
                if (playlists.Count > 0)
                {
                    Random random = new();
                    string selectedPlaylist = playlists[random.Next(playlists.Count)];
                    string playlistFilePath = FindPlaylistFilePath(selectedPlaylist);
                    if (!string.IsNullOrEmpty(playlistFilePath))
                    {
                        links.Clear();
                        playlists.Clear();
                        ProcessBatchFile(playlistFilePath, links, playlists);
                        SelectRandomLink(links);
                    }
                }
                else
                {
                    Console.WriteLine("No playlists available with links.");
                }
            }
        }

        private static string? FindBatchFilePath()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            foreach (string file in Directory.GetFiles(currentDirectory, "!!rnd_mix.bat", SearchOption.TopDirectoryOnly))
            {
                return file;
            }
            return null;
        }

        private static void ProcessBatchFile(string filePath, List<string> links, List<string> playlists)
        {
            bool startReading = false;
            foreach (string line in File.ReadLines(filePath))
            {
                string trimmedLine = line.Trim();
                if (trimmedLine == "GOTO :PROGRAM_START")
                {
                    startReading = true;
                    continue;
                }
                if (trimmedLine == ":PROGRAM_START")
                {
                    break;
                }

                if (startReading)
                {
                    if (LinkRegex.IsMatch(trimmedLine))
                    {
                        links.Add(trimmedLine);
                    }
                    else if (!string.IsNullOrEmpty(trimmedLine))
                    {
                        playlists.Add(trimmedLine);
                    }
                }
            }
        }

        private static string? FindPlaylistFilePath(string playlistName)
        {
            foreach (string directory in Directory.GetDirectories(Directory.GetCurrentDirectory(), "*", SearchOption.AllDirectories))
            {
                foreach (string file in Directory.GetFiles(directory, "!!rnd_mix.bat", SearchOption.TopDirectoryOnly))
                {
                    if (directory.EndsWith(playlistName, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }
            }
            return null;
        }

        private static void SelectRandomLink(List<string> links)
        {
            if (links.Count == 0)
            {
                Console.WriteLine("No links found in the batch file.");
                return;
            }

            Random random = new();
            string selectedLink = links[random.Next(links.Count)];

            if (selectedLink.Contains("youtube.com"))
            {
                OpenYouTubeLink(selectedLink);
            }
            else if (selectedLink.Contains("spotify.com"))
            {
                OpenSpotifyLink(selectedLink);
            }
        }

        private static void OpenYouTubeLink(string url)
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

        private static void OpenSpotifyLink(string url)
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