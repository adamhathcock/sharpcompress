namespace SharpCompress.Archives.Extraction;

internal enum ArchiveConcurrencyMode
{
    SequentialOnly,
    IndependentEntries,
    SolidBlocks,
    Mixed,
}
