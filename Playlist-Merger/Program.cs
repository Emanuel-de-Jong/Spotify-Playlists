using Playlist_Merger.Helpers;
using Playlist_Merger.Models;
using SpotifyAPI.Web;
using System.Net;
using YamlDotNet.Serialization;

namespace Playlist_Merger
{
    public class Program
    {
        private YAMLHelper yamlHelper = new();
        private SpotifyAPIHelper spotifyAPIHelper = new();

        Dictionary<string, string> cache;

        Dictionary<string, string> apiCredentials;
        private SpotifyClient spotifyClient;
        private string? userId;

        private Playlists oldMixPlaylists = [];
        private Playlists oldMergePlaylists = [];
        private Playlists mixPlaylists = [];
        private Playlists mergePlaylists;

        private static void Main(string[] args)
        {
            new Program().Run().GetAwaiter().GetResult();
        }

        private async Task Run()
        {
            try
            {
                Console.WriteLine("Loading YAMLs");
                cache = yamlHelper.LoadCache();
                mergePlaylists = yamlHelper.LoadMergePlaylistsDeps();

                oldMixPlaylists = yamlHelper.Deserialize<Playlists>(YAMLHelper.MIX_FILE_NAME);
                oldMixPlaylists ??= [];

                oldMergePlaylists = yamlHelper.Deserialize<Playlists>(YAMLHelper.MERGE_FILE_NAME);
                oldMergePlaylists ??= [];

                Console.WriteLine("Creating Spotify client");
                await CreateSpotifyClient();

                Console.WriteLine("Getting user id");
                await GetUserId();

                Console.WriteLine("Fetching playlist metas");
                await spotifyAPIHelper.FetchPlaylistMetas(mixPlaylists, mergePlaylists);

                Console.WriteLine("Creating missing merge playlists");
                await spotifyAPIHelper.CreateMergePlaylists(mergePlaylists);

                Console.WriteLine("Getting mix playlist tracks");
                await GetPlaylistTracks(mixPlaylists, oldMixPlaylists);
                yamlHelper.Serialize(mixPlaylists, YAMLHelper.MIX_FILE_NAME);

                Console.WriteLine("Getting merge playlist tracks");
                await GetPlaylistTracks(mergePlaylists, oldMergePlaylists);
                yamlHelper.Serialize(mergePlaylists, YAMLHelper.MERGE_FILE_NAME);

                Console.WriteLine("Updating merge playlists");
                await spotifyAPIHelper.UpdateMergePlaylists(mixPlaylists, mergePlaylists);
                yamlHelper.Serialize(mergePlaylists, YAMLHelper.MERGE_FILE_NAME);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.StackTrace);
            }

            yamlHelper.SaveAll(cache, apiCredentials, mixPlaylists, mergePlaylists);

            Console.WriteLine($"Request count: {spotifyAPIHelper.RequestCount}");
            Console.WriteLine("Done!");
        }

        private async Task CreateSpotifyClient()
        {
            apiCredentials = yamlHelper.Deserialize<Dictionary<string, string>>(YAMLHelper.SPOTIFY_API_FILE_NAME);
            spotifyClient = await spotifyAPIHelper.CreateSpotifyClient(apiCredentials);
            yamlHelper.Serialize(apiCredentials, YAMLHelper.SPOTIFY_API_FILE_NAME);
        }

        private async Task GetUserId()
        {
            if (!cache.TryGetValue("UserId", out userId))
            {
                userId = (await spotifyAPIHelper.Call(
                    () => spotifyClient.UserProfile.Current())).Id;
                cache["UserId"] = userId;
                yamlHelper.Serialize(cache, YAMLHelper.CACHE_FILE_NAME);
            }

            spotifyAPIHelper.UserId = userId;
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
    }
}
