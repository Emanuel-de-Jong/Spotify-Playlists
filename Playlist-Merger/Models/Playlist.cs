using SpotifyAPI.Web;
using System.ComponentModel.DataAnnotations.Schema;

namespace Playlist_Merger.Models
{
    public class Playlist
    {
        public string? Id { get; set; }
        public string SnapshotId { get; set; }
        [NotMapped]
        public bool? IsInclusive { get; set; }
        [NotMapped]
        public List<string>? Deps { get; set; }
        public List<string> Tracks { get; set; } = [];

        public void LoadFromResponse(FullPlaylist responsePlaylist)
        {
            Id = responsePlaylist.Id;
            SnapshotId = responsePlaylist.SnapshotId;
        }
    }
}
