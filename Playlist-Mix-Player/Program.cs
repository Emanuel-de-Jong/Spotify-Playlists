using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Playlist_Mix_Player
{
    public enum EMixChoiceOption
    {
        Link,
        Playlist
    }

    public class Program
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, uint processInformationLength, out uint returnLength);

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

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

            string batFilePath = FindBatchFilePathFromInvoker();
            if (string.IsNullOrEmpty(batFilePath) || !File.Exists(batFilePath))
            {
                Console.WriteLine("Error: Batch file not found.");
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
                    else
                    {
                        Console.WriteLine("Error: Playlist file not found for the selected playlist.");
                    }
                }
                else
                {
                    Console.WriteLine("No playlists available with links.");
                }
            }
        }

        private static string? FindBatchFilePathFromInvoker()
        {
            int parentPid = GetParentProcessId();
            if (parentPid > 0)
            {
                try
                {
                    using (Process parentProcess = Process.GetProcessById(parentPid))
                    {
                        if (parentProcess.MainModule?.FileName != null && parentProcess.MainModule.FileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                        {
                            return parentProcess.MainModule.FileName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error finding parent process: " + ex.Message);
                }
            }
            return null;
        }

        private static int GetParentProcessId()
        {
            PROCESS_BASIC_INFORMATION pbi = new();
            int status = NtQueryInformationProcess(GetCurrentProcess(), 0, ref pbi, (uint)Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)), out uint returnLength);
            if (status == 0)
            {
                return (int)pbi.InheritedFromUniqueProcessId;
            }
            return -1;
        }

        private static void ProcessBatchFile(string filePath, List<string> links, List<string> playlists)
        {
            bool startReading = false;
            foreach (string line in File.ReadLines(filePath))
            {
                string trimmedLine = line.Trim();
                if (trimmedLine == "GOTO PROGRAM_START")
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
                    if (trimmedLine.Contains("http"))
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
            else
            {
                Console.WriteLine("Error: Unsupported link format.");
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
