#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilites;

namespace SharpCompress.Common.SevenZip;

internal class ArchiveDatabase
{
    internal byte _majorVersion;
    internal byte _minorVersion;
    internal long _startPositionAfterHeader;
    internal long _dataStartPosition;

    internal List<long> _packSizes = new();
    internal List<uint?> _packCrCs = new();
    internal List<CFolder> _folders = new();
    internal List<int> _numUnpackStreamsVector;
    internal List<CFileItem> _files = new();

    internal List<long> _packStreamStartPositions = new();
    internal List<int> _folderStartFileIndex = new();
    internal List<int> _fileIndexToFolderIndexMap = new();

    private readonly Dictionary<CFolder, (Stream stream, long position)> _cachedFolderStreams =
        new();

    internal IPasswordProvider PasswordProvider { get; }

    public ArchiveDatabase(IPasswordProvider passwordProvider) =>
        PasswordProvider = passwordProvider;

    internal void Clear()
    {
        _packSizes.Clear();
        _packCrCs.Clear();
        _folders.Clear();
        _numUnpackStreamsVector = null;
        _files.Clear();

        _packStreamStartPositions.Clear();
        _folderStartFileIndex.Clear();
        _fileIndexToFolderIndexMap.Clear();

        foreach (var cached in _cachedFolderStreams.Values)
        {
            cached.stream.Dispose();
        }
        _cachedFolderStreams.Clear();
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
                        throw new InvalidOperationException();
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
        long skipSize,
        long fileSize
    )
    {
        // Check if we already have a cached decoder stream for this folder
        if (_cachedFolderStreams.TryGetValue(folder, out var cached))
        {
            // Calculate how much more we need to skip from the current position
            var additionalSkip = skipSize - cached.position;
            if (additionalSkip > 0)
            {
                cached.stream.Skip(additionalSkip);
            }
            else if (additionalSkip < 0)
            {
                // Stream position is ahead of where we need to be - can't seek backward
                // This shouldn't happen with sequential reading, but just in case,
                // dispose the old stream and create a new one
                cached.stream.Dispose();
                _cachedFolderStreams.Remove(folder);
            }
            else
            {
                // Position is exactly right, no skip needed
                // Update the cached position to account for the file that will be read
                _cachedFolderStreams[folder] = (cached.stream, skipSize + fileSize);
                return cached.stream;
            }

            if (additionalSkip >= 0)
            {
                // Update the cached position to account for the file that will be read
                _cachedFolderStreams[folder] = (cached.stream, skipSize + fileSize);
                return cached.stream;
            }
        }

        var packStreamIndex = folder._firstPackStreamId;
        var folderStartPackPos = GetFolderStreamPos(folder, 0);
        var count = folder._packStreams.Count;
        var packSizes = new long[count];
        for (var j = 0; j < count; j++)
        {
            packSizes[j] = _packSizes[packStreamIndex + j];
        }

        var decoderStream = DecoderStreamHelper.CreateDecoderStream(
            stream,
            folderStartPackPos,
            packSizes,
            folder,
            pw
        );

        // Skip to the requested position
        if (skipSize > 0)
        {
            decoderStream.Skip(skipSize);
        }

        // Cache the decoder stream for reuse, tracking position after this file is read
        _cachedFolderStreams[folder] = (decoderStream, skipSize + fileSize);

        return decoderStream;
    }
}
