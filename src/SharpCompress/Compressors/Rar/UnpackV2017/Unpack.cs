using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar.Headers;
using size_t = System.UInt32;

namespace SharpCompress.Compressors.Rar.UnpackV2017;

internal partial class Unpack : IRarUnpack
{
    private FileHeader fileHeader;
    private Stream readStream;
    private Stream writeStream;

    private void _UnpackCtor()
    {
        for (var i = 0; i < AudV.Length; i++)
        {
            AudV[i] = new AudioVariables();
        }
    }

    private int UnpIO_UnpRead(byte[] buf, int offset, int count) =>
        // NOTE: caller has logic to check for -1 for error we throw instead.
        readStream.Read(buf, offset, count);

    private async Task<int> UnpIO_UnpReadAsync(
        byte[] buf,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    ) =>
        // NOTE: caller has logic to check for -1 for error we throw instead.
        await readStream.ReadAsync(buf, offset, count, cancellationToken).ConfigureAwait(false);

    private void UnpIO_UnpWrite(byte[] buf, size_t offset, uint count) =>
        writeStream.Write(buf, checked((int)offset), checked((int)count));

    private async Task UnpIO_UnpWriteAsync(
        byte[] buf,
        size_t offset,
        uint count,
        CancellationToken cancellationToken = default
    ) =>
        await writeStream
            .WriteAsync(buf, checked((int)offset), checked((int)count), cancellationToken)
            .ConfigureAwait(false);

    public void DoUnpack(FileHeader fileHeader, Stream readStream, Stream writeStream)
    {
        // as of 12/2017 .NET limits array indexing to using a signed integer
        // MaxWinSize causes unpack to use a fragmented window when the file
        // window size exceeds MaxWinSize
        // uggh, that's not how this variable is used, it's the size of the currently allocated window buffer
        //x MaxWinSize = ((uint)int.MaxValue) + 1;

        // may be long.MaxValue which could indicate unknown size (not present in header)
        DestUnpSize = fileHeader.UncompressedSize;
        this.fileHeader = fileHeader;
        this.readStream = readStream;
        this.writeStream = writeStream;
        if (!fileHeader.IsStored)
        {
            Init(fileHeader.WindowSize, fileHeader.IsSolid);
        }
        Suspended = false;
        DoUnpack();
    }

    public async Task DoUnpackAsync(
        FileHeader fileHeader,
        Stream readStream,
        Stream writeStream,
        CancellationToken cancellationToken = default
    )
    {
        DestUnpSize = fileHeader.UncompressedSize;
        this.fileHeader = fileHeader;
        this.readStream = readStream;
        this.writeStream = writeStream;
        if (!fileHeader.IsStored)
        {
            Init(fileHeader.WindowSize, fileHeader.IsSolid);
        }
        Suspended = false;
        await DoUnpackAsync(cancellationToken).ConfigureAwait(false);
    }

    public void DoUnpack()
    {
        if (fileHeader.IsStored)
        {
            UnstoreFile();
        }
        else
        {
            DoUnpack(fileHeader.CompressionAlgorithm, fileHeader.IsSolid);
        }
    }

    public async Task DoUnpackAsync(CancellationToken cancellationToken = default)
    {
        if (fileHeader.IsStored)
        {
            await UnstoreFileAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // TODO: When compression methods are converted to async, call them here
            // For now, fall back to synchronous version
            await DoUnpackAsync(
                    fileHeader.CompressionAlgorithm,
                    fileHeader.IsSolid,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private void UnstoreFile()
    {
        Span<byte> b = stackalloc byte[(int)Math.Min(0x10000, DestUnpSize)];
        do
        {
            var n = readStream.Read(b);
            if (n == 0)
            {
                break;
            }
            writeStream.Write(b.Slice(0, n));
            DestUnpSize -= n;
        } while (!Suspended);
    }

    private async Task UnstoreFileAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[(int)Math.Min(0x10000, DestUnpSize)];
        do
        {
            var n = await readStream
                .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);
            if (n == 0)
            {
                break;
            }
            await writeStream.WriteAsync(buffer, 0, n, cancellationToken).ConfigureAwait(false);
            DestUnpSize -= n;
        } while (!Suspended);
    }

    public bool Suspended { get; set; }

    public long DestSize => DestUnpSize;

    public int Char
    {
        get
        {
            // TODO: coderb: not sure where the "MAXSIZE-30" comes from, ported from V1 code
            if (InAddr > MAX_SIZE - 30)
            {
                UnpReadBuf();
            }
            return InBuf[InAddr++];
        }
    }

    public int PpmEscChar
    {
        get => PPMEscChar;
        set => PPMEscChar = value;
    }

    public static byte[] EnsureCapacity(byte[] array, int length) =>
        array.Length < length ? new byte[length] : array;
}
