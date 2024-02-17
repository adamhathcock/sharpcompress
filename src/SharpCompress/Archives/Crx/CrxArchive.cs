using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using SharpCompress.Archives.Zip;
using System.Text;
using System.Reflection;

namespace SharpCompress.Archives.Crx;

public class CrxArchive : ZipArchive
{
    private const int MINIMUM_CRX_HEADER_LENGTH = 16;

    private string _tempFilename;
    private FileStream _fileStream;

    /// <summary>
    /// Constructor with a SourceStream able to handle SourceStreams.
    /// </summary>
    /// <param name="srcStream"></param>
    internal CrxArchive(SourceStream srcStream, FileStream fileStream, string tempFilename)
        : base(srcStream)
    {
        _fileStream = fileStream;
        _tempFilename = tempFilename;
    }

    public override void Dispose()
    {
        _fileStream.Dispose();

        File.Delete(_tempFilename);

        base.Dispose();
    }

    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="readerOptions"></param>
    public new static ZipArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.CheckNotNullOrEmpty(nameof(filePath));

        var stream = File.Open(filePath, FileMode.Open);

        return Open(stream, readerOptions);
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    public new static ZipArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.CheckNotNull(nameof(fileInfo));
        return Open(fileInfo.FullName, readerOptions);
    }

    /// <summary>
    /// Takes a seekable Stream as a source
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    public new static CrxArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.CheckNotNull(nameof(stream));


        if (stream.Length < MINIMUM_CRX_HEADER_LENGTH)
        {
            throw new ArchiveException(
                "Could not find Crx file header at the begin of the file. File may be corrupted."
            );
        }

        var buffer = new byte[4];
        stream.Read(buffer, 0, buffer.Length);
        if (Encoding.ASCII.GetString(buffer) != "Cr24")
            throw new ArchiveException("Invalid Crx file header");

        stream.Read(buffer, 0, buffer.Length);
        var version = BitConverter.ToUInt32(buffer, 0);
        if (version != 3)
            throw new ArchiveException(string.Format("Invalid Crx version ({0}). Only Crx version 3 is supported.", version));

        stream.Read(buffer, 0, buffer.Length);
        var headerLength = BitConverter.ToUInt32(buffer, 0);
        if (stream.Length < stream.Position + headerLength)
            throw new ArchiveException(string.Format("Invalid Crx header length ({0}).", headerLength));

        stream.Seek(headerLength, SeekOrigin.Current);


        var tempFilename = Path.GetTempFileName();
        File.Delete(tempFilename);

        var fileStream = File.Open(tempFilename, FileMode.Create);
        stream.CopyTo(fileStream);
        fileStream.Seek(0, SeekOrigin.Begin);


        return new CrxArchive(
            new SourceStream(fileStream, i => null, readerOptions ?? new ReaderOptions()),
            fileStream,
            tempFilename
        );
    }

    public static bool IsCrxFile(string filePath, string? password = null) =>
        IsCrxFile(new FileInfo(filePath), password);

    public static bool IsCrxFile(FileInfo fileInfo, string? password = null)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsCrxFile(stream, password);
    }

    public static bool IsCrxFile(Stream stream, string? password = null)
    {
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            var header = headerFactory
                .ReadStreamHeader(stream)
                .FirstOrDefault(x => x.ZipHeaderType != ZipHeaderType.Split);
            if (header is null)
            {
                return false;
            }
            return Enum.IsDefined(typeof(ZipHeaderType), header.ZipHeaderType);
        }
        catch (CryptographicException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
    protected override IEnumerable<ZipVolume> LoadVolumes(SourceStream srcStream)
    {
        return new ZipVolume(SrcStream, ReaderOptions, 0).AsEnumerable();
    }
}
