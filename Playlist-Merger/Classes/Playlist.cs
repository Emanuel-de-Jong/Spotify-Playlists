using SpotifyAPI.Web;
using System.ComponentModel.DataAnnotations.Schema;

namespace Playlist_Merger.Classes
{
    public class Playlist
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SnapshotId { get; set; }
        [NotMapped]
        public bool? IsInclusive { get; set; }
        [NotMapped]
        public List<Playlist>? Deps { get; set; }
        public List<string> Tracks { get; set; } = [];

        public Playlist() { }

        public Playlist(FullPlaylist responsePlaylist)
        {
            Id = responsePlaylist.Id;
            Name = responsePlaylist.Name;
            SnapshotId = responsePlaylist.SnapshotId;
        }
    }
}
