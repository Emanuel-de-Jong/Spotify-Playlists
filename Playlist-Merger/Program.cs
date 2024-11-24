using Playlist_Merger.Helpers;
using Playlist_Merger.Models;
using SpotifyAPI.Web;
using System.Text;

namespace Playlist_Merger
{
    public class Program
    {
#if DEBUG
        private bool isManual = true;
#else
        private bool isManual = false;
#endif

        private YAMLHelper? yamlHelper;
        private SpotifyAPIHelper? spotifyAPIHelper;

        private Dictionary<string, string>? cache;
        private Dictionary<string, string>? apiCredentials;
        private SpotifyClient? spotifyClient;
        private string? userId;

        private Playlists? oldMixPlaylists;
        private Playlists? oldMergePlaylists;
        private Playlists? mixPlaylists;
        private Playlists? mergePlaylists;

        private static void Main(string[] args)
        {
            new Program().Run(args).GetAwaiter().GetResult();
        }

        private async Task Run(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "manual")
                {
                    isManual = true;
                }

                yamlHelper = new();
                spotifyAPIHelper = new();
                mixPlaylists = [];

                Console.WriteLine("Loading YAMLs");
                bool isSuccess = LoadYAMLs();
                if (!isSuccess)
                {
                    return;
                }

                Console.WriteLine("Creating Spotify client");
                await CreateSpotifyClient();

                Console.WriteLine("Getting user id");
                await GetUserId();

                Console.WriteLine("Fetching playlist metas");
                await FetchPlaylistMetas(mixPlaylists, mergePlaylists);

                UpdateDeps();

                Console.WriteLine("Creating missing merge playlists");
                await CreateMergePlaylists(mergePlaylists);

                Console.WriteLine("Getting mix playlist tracks");
                await GetPlaylistTracks(mixPlaylists, oldMixPlaylists);

                Console.WriteLine("Getting merge playlist tracks");
                await GetPlaylistTracks(mergePlaylists, oldMergePlaylists);

                Console.WriteLine("Updating merge playlists");
                await UpdateMergePlaylists(mixPlaylists, mergePlaylists);
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }

            Console.WriteLine("Saving changes locally");
            yamlHelper.SaveAll(cache, apiCredentials, mixPlaylists, mergePlaylists);

            Console.WriteLine($"Request count: {spotifyAPIHelper.RequestCount}");
            Console.WriteLine("Done!");
        }

        private bool LoadYAMLs()
        {
            cache = yamlHelper.LoadCache();
            if (cache.ContainsKey("Problem"))
            {
                return false;
            }

            mergePlaylists = yamlHelper.LoadMergePlaylistsDeps();

            oldMixPlaylists = yamlHelper.Deserialize<Playlists>(YAMLHelper.MIX_FILE_NAME);
            oldMixPlaylists ??= [];

            oldMergePlaylists = yamlHelper.Deserialize<Playlists>(YAMLHelper.MERGE_FILE_NAME);
            oldMergePlaylists ??= [];

            return true;
        }

        private async Task CreateSpotifyClient()
        {
            apiCredentials = yamlHelper.Deserialize<Dictionary<string, string>>(YAMLHelper.SPOTIFY_API_FILE_NAME);
            spotifyClient = await spotifyAPIHelper.CreateSpotifyClient(apiCredentials, isManual);
        }

        private async Task GetUserId()
        {
            if (!cache.TryGetValue("UserId", out userId))
            {
                userId = (await spotifyAPIHelper.Call(
                    () => spotifyClient.UserProfile.Current())).Id;
                cache["UserId"] = userId;
            }
        }

        public async Task FetchPlaylistMetas(Playlists mixPlaylists, Playlists mergePlaylists)
        {
            PlaylistCurrentUsersRequest request = new() { Limit = 50 };
            List<FullPlaylist> responsePlaylists = await spotifyAPIHelper.CallPaginated(
                () => spotifyClient.Playlists.CurrentUsers(request));
            foreach (FullPlaylist responsePlaylist in responsePlaylists)
            {
                if (responsePlaylist.Owner.Id != userId)
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

        public void UpdateDeps()
        {
            foreach (Playlist mergePlaylist in mergePlaylists.Values)
            {
                if (!mergePlaylist.IsInclusive)
                {
                    mergePlaylist.Deps = mixPlaylists.Keys.Where(n => !mergePlaylist.Deps.Contains(n)).ToList();
                    mergePlaylist.IsInclusive = true;
                }
            }

            foreach (Playlist mergePlaylist in mergePlaylists.Values)
            {
                foreach (string mergeDep in mergePlaylist.MergeDeps)
                {
                    mergePlaylist.Deps.AddRange(mergePlaylists[mergeDep].Deps);
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
                    FullPlaylist responsePlaylist = await spotifyAPIHelper.Call(
                        () => spotifyClient.Playlists.Create(userId, request));

                    mergePlaylists.Add(responsePlaylist);
                }
            }
        }

        private async Task GetPlaylistTracks(Playlists playlists, Playlists oldPlaylists)
        {
            foreach (string playlistName in playlists.Keys)
            {
                Playlist playlist = playlists[playlistName];
                oldPlaylists.TryGetValue(playlistName, out Playlist? oldPlaylist);
                if (oldPlaylist != null &&
                    oldPlaylist.Id == playlist.Id &&
                    oldPlaylist.SnapshotId == playlist.SnapshotId)
                {
                    playlist.Tracks = oldPlaylist.Tracks;
                    continue;
                }

                PlaylistGetItemsRequest request = new() { Limit = 100 };
                List<PlaylistTrack<IPlayableItem>> items = await spotifyAPIHelper.CallPaginated(
                    () => spotifyClient.Playlists.GetItems(playlist.Id, request));

                playlist.Tracks = items
                    .Select(i => i.Track)
                    .OfType<FullTrack>()
                    .Select(track => track.Id)
                    .ToList();
            }
        }

        public async Task UpdateMergePlaylists(Playlists mixPlaylists, Playlists mergePlaylists)
        {
            foreach (Playlist mergePlaylist in mergePlaylists.Values)
            {
                List<string>? newTracks = mergePlaylist.Deps
                    .SelectMany(dep => mixPlaylists[dep].Tracks)
                    .ToList();

                List<string> addedTracks = newTracks.Except(mergePlaylist.Tracks).ToList();
                for (int i = 0; i < addedTracks.Count; i += 100)
                {
                    List<string> batch = addedTracks.Skip(i).Take(100).ToList();
                    await spotifyAPIHelper.AddTracksToPlaylist(batch, mergePlaylist);
                }

                List<string> removedTracks = mergePlaylist.Tracks.Except(newTracks).ToList();
                for (int i = 0; i < removedTracks.Count; i += 100)
                {
                    List<string> batch = removedTracks.Skip(i).Take(100).ToList();
                    await spotifyAPIHelper.RemoveTracksFromPlaylist(batch, mergePlaylist);
                }
            }
        }

        public void HandleException(Exception ex)
        {
            StringBuilder stringBuilder = new();
            string date = DateTime.Now.ToString("dd-MM-yy HH:mm");
            stringBuilder.AppendLine($"[{date}] {ex.GetType()}: {ex.Message}");
            stringBuilder.AppendLine($"Requests: {spotifyAPIHelper?.RequestCount}");
            stringBuilder.AppendLine(ex.ToString());
            stringBuilder.AppendLine(ex.StackTrace);

            string errorMessage = stringBuilder.ToString();
            Console.WriteLine(errorMessage);
            File.AppendAllText("log.txt", errorMessage);

            cache["Problem"] = "";
        }
    }
}
