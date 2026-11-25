namespace SharpCompress.IO;

/// <summary>
/// A decompression stream whose decoder can fall out of sync with its input, after which
/// neither further reads nor skipping to the end of the entry are possible.
/// </summary>
internal interface IFaultableStream
{
    bool IsFaulted { get; }
}
