namespace SharpCompress.Common.Arj.Headers
{
    public enum FileType : byte
    {
        Binary = 0,
        Text7Bit = 1,
        CommentHeader = 2,
        Directory = 3,
        VolumeLabel = 4,
        ChapterLabel = 5,
        Unknown = 255,
    }
}
