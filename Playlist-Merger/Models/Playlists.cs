using SpotifyAPI.Web;

namespace Playlist_Merger.Models
{
    public class Playlists : Dictionary<string, Playlist>
    {
        public void Add(FullPlaylist responsePlaylist)
        {
            if (!ContainsKey(responsePlaylist.Name))
            {
                this[responsePlaylist.Name] = new();
            }

            this[responsePlaylist.Name].LoadFromResponse(responsePlaylist);
        }
    }
}
