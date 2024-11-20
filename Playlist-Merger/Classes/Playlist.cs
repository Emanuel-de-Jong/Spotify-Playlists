namespace Playlist_Merger.Classes
{
    public class Playlist
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SnapshotId { get; set; }
        public bool? IsInclusive { get; set; }
        public List<Playlist>? Deps { get; set; }
        public List<string> Tracks { get; set; } = [];
    }
}
