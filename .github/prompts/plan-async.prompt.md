# Plan: Implement Missing Async Functionality in SharpCompress

SharpCompress has async support for low-level stream operations and Reader/Writer APIs, but critical entry points (Archive.Open, factory methods, initialization) remain synchronous. This plan adds async overloads for all user-facing I/O operations and fixes existing async bugs, enabling full end-to-end async workflows.

## Steps

1. **Add async factory methods** to [ArchiveFactory.cs](src/SharpCompress/Factories/ArchiveFactory.cs), [ReaderFactory.cs](src/SharpCompress/Factories/ReaderFactory.cs), and [WriterFactory.cs](src/SharpCompress/Factories/WriterFactory.cs) with `OpenAsync` and `CreateAsync` overloads accepting `CancellationToken`

2. **Implement async Open methods** on concrete archive types ([ZipArchive.cs](src/SharpCompress/Archives/Zip/ZipArchive.cs), [TarArchive.cs](src/SharpCompress/Archives/Tar/TarArchive.cs), [RarArchive.cs](src/SharpCompress/Archives/Rar/RarArchive.cs), [GZipArchive.cs](src/SharpCompress/Archives/GZip/GZipArchive.cs), [SevenZipArchive.cs](src/SharpCompress/Archives/SevenZip/SevenZipArchive.cs)) and reader types ([ZipReader.cs](src/SharpCompress/Readers/Zip/ZipReader.cs), [TarReader.cs](src/SharpCompress/Readers/Tar/TarReader.cs), etc.)

3. **Convert archive initialization logic to async** including header reading, volume loading, and format signature detection across archive constructors and internal initialization methods

4. **Fix LZMA decoder async bugs** in [LzmaStream.cs](src/SharpCompress/Compressors/LZMA/LzmaStream.cs), [Decoder.cs](src/SharpCompress/Compressors/LZMA/Decoder.cs), and [OutWindow.cs](src/SharpCompress/Compressors/LZMA/OutWindow.cs) to enable true async 7Zip support and remove `NonDisposingStream` workaround

5. **Complete Rar async implementation** by converting `UnpackV2017` methods to async in [UnpackV2017.cs](src/SharpCompress/Compressors/Rar/UnpackV2017.cs) and updating Rar20 decompression

6. **Add comprehensive async tests** covering all new async entry points, cancellation scenarios, and concurrent operations across all archive formats in test files

## Further Considerations

1. **Breaking changes** - Should new async methods be added alongside existing sync methods (non-breaking), or should sync methods eventually be deprecated? Recommend additive approach for backward compatibility.

2. **Performance impact** - Header parsing for formats like Zip/Tar is often small; consider whether truly async parsing adds value vs sync parsing wrapped in Task, or make it conditional based on stream type (network vs file).

3. **7Zip complexity** - The LZMA async bug fix (Step 4) may be challenging due to state management in the decoder; consider whether to scope it separately or implement a simpler workaround that maintains correctness.
