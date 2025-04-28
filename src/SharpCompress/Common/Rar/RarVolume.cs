using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Rar;

/// <summary>
/// A RarArchiveVolume is a single rar file that may or may not be a split RarArchive.  A Rar Archive is one to many Rar Parts
/// </summary>
public abstract class RarVolume : Volume
{
    private readonly RarHeaderFactory _headerFactory;
    private int _maxCompressionAlgorithm;

    internal RarVolume(StreamingMode mode, Stream stream, ReaderOptions options, int index)
        : base(stream, options, index) => _headerFactory = new RarHeaderFactory(mode, options);

    private ArchiveHeader? ArchiveHeader { get; set; }

    private StreamingMode Mode => _headerFactory.StreamingMode;

    internal abstract IEnumerable<RarFilePart> ReadFileParts();

    internal abstract RarFilePart CreateFilePart(MarkHeader markHeader, FileHeader fileHeader);

    internal IEnumerable<RarFilePart> GetVolumeFileParts()
    {
        MarkHeader? lastMarkHeader = null;
        foreach (var header in _headerFactory.ReadHeaders(Stream))
        {
            switch (header.HeaderType)
            {
                case HeaderType.Mark:
                    {
                        lastMarkHeader = (MarkHeader)header;
                    }
                    break;
                case HeaderType.Archive:
                    {
                        ArchiveHeader = (ArchiveHeader)header;
                    }
                    break;
                case HeaderType.File:
                    {
                        var fh = (FileHeader)header;
                        if (_maxCompressionAlgorithm < fh.CompressionAlgorithm)
                        {
                            _maxCompressionAlgorithm = fh.CompressionAlgorithm;
                        }

                        yield return CreateFilePart(lastMarkHeader!, fh);
                    }
                    break;
                case HeaderType.Service:
                    {
                        var fh = (FileHeader)header;
                        if (fh.FileName == "CMT")
                        {
                            var buffer = new byte[fh.CompressedSize];
                            fh.PackedStream.Read(buffer, 0, buffer.Length);
                            Comment = Encoding.UTF8.GetString(buffer, 0, buffer.Length - 1);
                        }
                    }
                    break;
            }
        }
    }

    private void EnsureArchiveHeaderLoaded()
    {
        if (ArchiveHeader is null)
        {
            if (Mode == StreamingMode.Streaming)
            {
                throw new InvalidOperationException(
                    "ArchiveHeader should never been null in a streaming read."
                );
            }

            // we only want to load the archive header to avoid overhead but have to do the nasty thing and reset the stream
            GetVolumeFileParts().First();
            Stream.Position = 0;
        }
    }

    /// <summary>
    /// RarArchive is the first volume of a multi-part archive.
    /// Only Rar 3.0 format and higher
    /// </summary>
    public override bool IsFirstVolume
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            return ArchiveHeader?.IsFirstVolume ?? false;
        }
    }

    /// <summary>
    /// RarArchive is part of a multi-part archive.
    /// </summary>
    public override bool IsMultiVolume
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            return ArchiveHeader?.IsVolume ?? false;
        }
    }

    /// <summary>
    /// RarArchive is SOLID (this means the Archive saved bytes by reusing information which helps for archives containing many small files).
    /// Currently, SharpCompress cannot decompress SOLID archives.
    /// </summary>
    public bool IsSolidArchive
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            return ArchiveHeader?.IsSolid ?? false;
        }
    }

    public int MinVersion
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            if (_maxCompressionAlgorithm >= 50)
            {
                return 5; //5-6
            }
            else if (_maxCompressionAlgorithm >= 29)
            {
                return 3; //3-4
            }
            else if (_maxCompressionAlgorithm >= 20)
            {
                return 2; //2
            }
            else
            {
                return 1;
            }
        }
    }

    public int MaxVersion
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            if (_maxCompressionAlgorithm >= 50)
            {
                return 6; //5-6
            }
            else if (_maxCompressionAlgorithm >= 29)
            {
                return 4; //3-4
            }
            else if (_maxCompressionAlgorithm >= 20)
            {
                return 2; //2
            }
            else
            {
                return 1;
            }
        }
    }

    public string? Comment { get; internal set; }
}
