namespace Playlist_Merger.Classes
{
    public class MergePlaylistDeps
    {
        public string Name { get; set; }
        public bool IsInclusive { get; set; } = true;
        public List<string> Deps { get; set; }
    }
}
