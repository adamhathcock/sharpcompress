using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilities;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip;

internal partial class ArchiveDatabase
{
    internal byte _majorVersion;
    internal byte _minorVersion;
    internal long _startPositionAfterHeader;
    internal long _dataStartPosition;

    internal List<long> _packSizes = new();
    internal List<uint?> _packCrCs = new();
    internal List<CFolder> _folders = new();
    internal List<int> _numUnpackStreamsVector = null!;
    internal List<CFileItem> _files = new();

    internal List<long> _packStreamStartPositions = new();
    internal List<int> _folderStartFileIndex = new();
    internal List<int> _fileIndexToFolderIndexMap = new();

    internal IPasswordProvider PasswordProvider { get; }

    public ArchiveDatabase(IPasswordProvider passwordProvider) =>
        PasswordProvider = passwordProvider;

    internal void Clear()
    {
        _packSizes.Clear();
        _packCrCs.Clear();
        _folders.Clear();
        _numUnpackStreamsVector = null!;
        _files.Clear();

        _packStreamStartPositions.Clear();
        _folderStartFileIndex.Clear();
        _fileIndexToFolderIndexMap.Clear();
    }

    internal bool IsEmpty() =>
        _packSizes.Count == 0
        && _packCrCs.Count == 0
        && _folders.Count == 0
        && _numUnpackStreamsVector.Count == 0
        && _files.Count == 0;

    private void FillStartPos()
    {
        _packStreamStartPositions.Clear();

        long startPos = 0;
        for (var i = 0; i < _packSizes.Count; i++)
        {
            _packStreamStartPositions.Add(startPos);
            startPos += _packSizes[i];
        }
    }

    private void FillFolderStartFileIndex()
    {
        _folderStartFileIndex.Clear();
        _fileIndexToFolderIndexMap.Clear();

        var folderIndex = 0;
        var indexInFolder = 0;
        for (var i = 0; i < _files.Count; i++)
        {
            var file = _files[i];

            var emptyStream = !file.HasStream;

            if (emptyStream && indexInFolder == 0)
            {
                _fileIndexToFolderIndexMap.Add(-1);
                continue;
            }

            if (indexInFolder == 0)
            {
                // v3.13 incorrectly worked with empty folders
                // v4.07: Loop for skipping empty folders
                for (; ; )
                {
                    if (folderIndex >= _folders.Count)
                    {
                        throw new ArchiveOperationException();
                    }

                    _folderStartFileIndex.Add(i); // check it

                    if (_numUnpackStreamsVector![folderIndex] != 0)
                    {
                        break;
                    }

                    folderIndex++;
                }
            }

            _fileIndexToFolderIndexMap.Add(folderIndex);

            if (emptyStream)
            {
                continue;
            }

            indexInFolder++;

            if (indexInFolder >= _numUnpackStreamsVector![folderIndex])
            {
                folderIndex++;
                indexInFolder = 0;
            }
        }
    }

    public void Fill()
    {
        FillStartPos();
        FillFolderStartFileIndex();
    }

    internal long GetFolderStreamPos(CFolder folder, int indexInFolder)
    {
        var index = folder._firstPackStreamId + indexInFolder;
        return _dataStartPosition + _packStreamStartPositions[index];
    }

    internal long GetFolderFullPackSize(int folderIndex)
    {
        var packStreamIndex = _folders[folderIndex]._firstPackStreamId;
        var folder = _folders[folderIndex];

        long size = 0;
        for (var i = 0; i < folder._packStreams.Count; i++)
        {
            size += _packSizes[packStreamIndex + i];
        }

        return size;
    }

    internal Stream GetFolderStream(
        Stream stream,
        CFolder folder,
        IPasswordProvider pw,
        bool enableParallelism = false
    )
    {
#if !LEGACY_DOTNET
        // Opportunistically decode the whole folder concurrently when it's a single, unchained,
        // unencrypted LZMA2 stream backed by a real (seekable, positional-I/O-capable) file -- the
        // only shape where independent per-block decoding is possible and worth the temp-file
        // round-trip. Opt-in only (via ReaderOptions.EnableParallelism): any other shape
        // (multi-coder chains, BCJ/delta filters, encryption, a non-file stream, a single-core
        // machine, or too few independent restart points to make parallelizing worthwhile) safely
        // falls back to the existing sequential decode below.
        if (
            enableParallelism
            && TryGetParallelDecodedFolderStream(stream, folder, out var parallelStream)
        )
        {
            return parallelStream!;
        }
#endif

        var packStreamIndex = folder._firstPackStreamId;
        var folderStartPackPos = GetFolderStreamPos(folder, 0);
        var count = folder._packStreams.Count;
        var packSizes = new long[count];
        for (var j = 0; j < count; j++)
        {
            packSizes[j] = _packSizes[packStreamIndex + j];
        }

        return DecoderStreamHelper.CreateDecoderStream(
            stream,
            folderStartPackPos,
            packSizes,
            folder,
            pw
        );
    }

#if !LEGACY_DOTNET
    /// <summary>
    /// Attempts to decode <paramref name="folder"/> entirely up front using
    /// <see cref="Lzma2ParallelDecoder"/>, writing the result to a temp file and returning a
    /// stream over it. Returns false (and leaves <paramref name="decodedStream"/> null) whenever
    /// the folder's shape, the host stream, or the current hardware make parallel decoding
    /// inapplicable or not worthwhile -- callers must fall back to sequential decoding.
    /// </summary>
    private bool TryGetParallelDecodedFolderStream(
        Stream stream,
        CFolder folder,
        out Stream? decodedStream
    )
    {
        decodedStream = null;

        // Matches 7-Zip's own behavior: with a single available core there is nothing to gain
        // from splitting work across threads, so skip straight to the sequential path.
        if (Environment.ProcessorCount <= 1)
        {
            return false;
        }

        // Only a lone, unchained LZMA2 coder can be split into independently-decodable blocks.
        // Any bind pairs (BCJ/delta filters, etc.) or additional coders (e.g. AES encryption)
        // mean the folder's bytes depend on more than just this one LZMA2 stream.
        if (
            folder._coders.Count != 1
            || folder._coders[0]._methodId != CMethodId.K_LZMA2
            || folder._packStreams.Count != 1
            || folder._bindPairs.Count != 0
        )
        {
            return false;
        }

        var props = folder._coders[0]._props;
        if (props is null || props.Length == 0)
        {
            return false;
        }

        // Parallel decoding needs positional (random-access) reads from the source file and
        // positional writes to the temp output file across multiple threads; a real, seekable
        // file handle is required for that. Streams the archive was opened from may be wrapped
        // (buffering, source-stream indirection, etc.), so unwrap looking for the underlying file.
        var inputFile = stream as FileStream ?? (stream as IStreamStack)?.GetStream<FileStream>();
        if (inputFile is null)
        {
            return false;
        }

        var folderIndex = _folders.IndexOf(folder);
        var packStart = GetFolderStreamPos(folder, 0);
        var packSize = GetFolderFullPackSize(folderIndex);
        var unpackSize = folder.GetUnpackSize();

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(stream, packStart, packSize, unpackSize);
        if (blocks is null || blocks.Count <= 1)
        {
            return false;
        }

        var tempFile = new FileStream(
            Path.GetTempFileName(),
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            FileOptions.DeleteOnClose
        );

        try
        {
            Lzma2ParallelDecoder.DecodeBlocksParallel(
                inputFile.SafeFileHandle,
                props,
                packStart,
                blocks,
                tempFile.SafeFileHandle,
                Math.Min(Environment.ProcessorCount, Lzma2ParallelDecoder.MaxThreads)
            );
        }
        catch
        {
            tempFile.Dispose();
            throw;
        }

        tempFile.Position = 0;
        decodedStream = tempFile;
        return true;
    }
#endif

    // Cache used to avoid re-decoding a solid folder from scratch for every file it contains.
    // Without this, extracting N files from the same solid folder decodes O(N^2) bytes since each
    // file's stream was previously created fresh from the folder start and skipped forward.
    private CFolder? _cachedFolder;
    private Stream? _cachedFolderStream;
    private long _cachedFolderStreamPosition;

    /// <summary>
    /// Returns a stream positioned at <paramref name="skipSize"/> bytes into the decompressed
    /// contents of <paramref name="folder"/>. Reuses a cached decoder stream for the folder when
    /// possible instead of recreating and re-decoding from the start.
    /// </summary>
    internal Stream GetFolderStream(
        Stream stream,
        CFolder folder,
        IPasswordProvider pw,
        long skipSize,
        long entrySize,
        bool enableParallelism = false
    )
    {
        if (_cachedFolder == folder && _cachedFolderStream != null)
        {
            if (skipSize >= _cachedFolderStreamPosition)
            {
                var delta = skipSize - _cachedFolderStreamPosition;
                if (delta > 0)
                {
                    _cachedFolderStream.Skip(delta);
                }
                // Assume the caller will fully consume the returned entry stream, advancing the
                // shared stream by entrySize bytes.
                _cachedFolderStreamPosition = skipSize + entrySize;
                return _cachedFolderStream;
            }

            // Non-sequential (backward) access within the same folder requires restarting.
            _cachedFolderStream.Dispose();
            _cachedFolderStream = null;
            _cachedFolder = null;
        }
        else if (_cachedFolderStream != null)
        {
            _cachedFolderStream.Dispose();
            _cachedFolderStream = null;
            _cachedFolder = null;
        }

        var newStream = GetFolderStream(stream, folder, pw, enableParallelism);
        if (skipSize > 0)
        {
            newStream.Skip(skipSize);
        }

        _cachedFolder = folder;
        _cachedFolderStream = newStream;
        _cachedFolderStreamPosition = skipSize + entrySize;
        return newStream;
    }

    internal void DisposeCachedFolderStream()
    {
        _cachedFolderStream?.Dispose();
        _cachedFolderStream = null;
        _cachedFolder = null;
        _cachedFolderStreamPosition = 0;
    }
}
