using Playlist_Merger.Models;
using YamlDotNet.Serialization;

namespace Playlist_Merger.Helpers
{
    public class YAMLHelper
    {
        public const string CACHE_FILE_NAME = "Cache";
        public const string DEPS_FILE_NAME = "Merge-Playlists-Deps";
        public const string MIX_FILE_NAME = "Mix-Playlists";
        public const string MERGE_FILE_NAME = "Merge-Playlists";
        public const string SPOTIFY_API_FILE_NAME = "Spotify-API";

        private ISerializer serializer;
        private IDeserializer deserializer;

        public YAMLHelper()
        {
            serializer = new SerializerBuilder().Build();
            deserializer = new DeserializerBuilder().Build();
        }

        public void Serialize<T>(T obj, string fileName)
        {
            string yamlContent = serializer.Serialize(obj);
            File.WriteAllText($"{Program.BASE_PATH}{fileName}.yaml", yamlContent);
        }

        public T? Deserialize<T>(string fileName)
        {
            string filePath = $"{Program.BASE_PATH}{fileName}.yaml";
            if (!File.Exists(filePath))
            {
                return default;
            }

            string yamlContent = File.ReadAllText(filePath);
            return deserializer.Deserialize<T>(yamlContent);
        }

        public Dictionary<string, string> LoadCache()
        {
            Dictionary<string, string>? cache = Deserialize<Dictionary<string, string>>(CACHE_FILE_NAME);
            cache ??= [];
            return cache;
        }

        public Playlists LoadMergePlaylistsDeps()
        {
            Playlists mergePlaylists = Deserialize<Playlists>(DEPS_FILE_NAME);
            foreach (Playlist mergePlaylist in mergePlaylists.Values)
            {
                mergePlaylist.Deps = mergePlaylist.Deps.Select(d => $"KBOT's {d} Mix").ToList();
            }
            return mergePlaylists;
        }

        public void SaveAll(Dictionary<string, string>? cache,
            Dictionary<string, string>? apiCredentials,
            Playlists? mixPlaylists,
            Playlists? mergePlaylists)
        {
            if (cache != null)
            {
                Serialize(cache, CACHE_FILE_NAME);
            }

            if (apiCredentials != null)
            {
                Serialize(apiCredentials, SPOTIFY_API_FILE_NAME);
            }

            if (mixPlaylists != null)
            {
                Serialize(mixPlaylists, MIX_FILE_NAME);
            }

            if (mergePlaylists != null)
            {
                Serialize(mergePlaylists, MERGE_FILE_NAME);
            }
        }
    }
}
