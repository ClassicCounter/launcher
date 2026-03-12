namespace Wauncher.ViewModels
{
    public class PatchNoteItem
    {
        public string Text { get; set; } = string.Empty;
        public bool IsMajorHeader { get; set; }
        public bool IsDateHeader { get; set; }
        public bool IsHeader { get; set; }
        public bool IsBullet { get; set; }
    }
}
