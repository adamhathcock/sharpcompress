using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Deflate;

public partial class GZipStream
{
    /// <summary>
    /// Flush the stream.
    /// </summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        await BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///   Read and decompress data from the source stream.
    /// </summary>
    ///
    /// <remarks>
    ///   With a <c>GZipStream</c>, decompression is done through reading.
    /// </remarks>
    ///
    /// <example>
    /// <code>
    /// byte[] working = new byte[WORKING_BUFFER_SIZE];
    /// using (System.IO.Stream input = System.IO.File.OpenRead(_CompressedFile))
    /// {
    ///     using (Stream decompressor= new Ionic.Zlib.GZipStream(input, CompressionMode.Decompress, true))
    ///     {
    ///         using (var output = System.IO.File.Create(_DecompressedFile))
    ///         {
    ///             int n;
    ///             while ((n= decompressor.Read(working, 0, working.Length)) !=0)
    ///             {
    ///                 output.Write(working, 0, n);
    ///             }
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <param name="buffer">The buffer into which the decompressed data should be placed.</param>
    /// <param name="offset">the offset within that data array to put the first byte read.</param>
    /// <param name="count">the number of bytes to read.</param>
    /// <returns>the number of bytes actually read</returns>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        var n = await BaseStream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);

        // Console.WriteLine("GZipStream::Read(buffer, off({0}), c({1}) = {2}", offset, count, n);
        // Console.WriteLine( Util.FormatByteArray(buffer, offset, n) );

        if (!_firstReadDone)
        {
            _firstReadDone = true;
            FileName = BaseStream._GzipFileName;
            Comment = BaseStream._GzipComment;
            LastModified = BaseStream._GzipMtime;
        }
        return n;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        var n = await BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (!_firstReadDone)
        {
            _firstReadDone = true;
            FileName = BaseStream._GzipFileName;
            Comment = BaseStream._GzipComment;
            LastModified = BaseStream._GzipMtime;
        }
        return n;
    }
#endif

    /// <summary>
    ///   Write data to the stream.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    ///   If you wish to use the <c>GZipStream</c> to compress data while writing,
    ///   you can create a <c>GZipStream</c> with <c>CompressionMode.Compress</c>, and a
    ///   writable output stream.  Then call <c>Write()</c> on that <c>GZipStream</c>,
    ///   providing uncompressed data as input.  The data sent to the output stream
    ///   will be the compressed form of the data written.
    /// </para>
    ///
    /// <para>
    ///   A <c>GZipStream</c> can be used for <c>Read()</c> or <c>Write()</c>, but not
    ///   both. Writing implies compression.  Reading implies decompression.
    /// </para>
    ///
    /// </remarks>
    /// <param name="buffer">The buffer holding data to write to the stream.</param>
    /// <param name="offset">the offset within that data array to find the first byte to write.</param>
    /// <param name="count">the number of bytes to write.</param>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        if (BaseStream._streamMode == ZlibBaseStream.StreamMode.Undefined)
        {
            //Console.WriteLine("GZipStream: First write");
            if (BaseStream._wantCompress)
            {
                // first write in compression, therefore, emit the GZIP header
                _headerByteCount = await EmitHeaderAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new ArchiveOperationException();
            }
        }

        await BaseStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }
        if (BaseStream._streamMode == ZlibBaseStream.StreamMode.Undefined)
        {
            if (BaseStream._wantCompress)
            {
                // first write in compression, therefore, emit the GZIP header
                _headerByteCount = await EmitHeaderAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new ArchiveOperationException();
            }
        }

        await BaseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif

#if !LEGACY_DOTNET || NETSTANDARD2_1
    public override async ValueTask DisposeAsync()
#else
    public async ValueTask DisposeAsync()
#endif
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (BaseStream != null)
        {
            await BaseStream.DisposeAsync().ConfigureAwait(false);
        }

#if !LEGACY_DOTNET || NETSTANDARD2_1
        await base.DisposeAsync().ConfigureAwait(false);
#endif
    }
}
