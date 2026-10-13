using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA.Utilities;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA;

internal abstract class DecoderStream2 : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}

internal static class DecoderStreamHelper
{
    private static int FindCoderIndexForOutStreamIndex(CFolder folderInfo, int outStreamIndex)
    {
        for (var coderIndex = 0; coderIndex < folderInfo._coders.Count; coderIndex++)
        {
            var coderInfo = folderInfo._coders[coderIndex];
            outStreamIndex -= coderInfo._numOutStreams;
            if (outStreamIndex < 0)
            {
                return coderIndex;
            }
        }

        throw new InvalidFormatException("Could not link output stream to coder.");
    }

    private static void FindPrimaryOutStreamIndex(
        CFolder folderInfo,
        out int primaryCoderIndex,
        out int primaryOutStreamIndex
    )
    {
        var foundPrimaryOutStream = false;
        primaryCoderIndex = -1;
        primaryOutStreamIndex = -1;

        for (
            int outStreamIndex = 0, coderIndex = 0;
            coderIndex < folderInfo._coders.Count;
            coderIndex++
        )
        {
            for (
                var coderOutStreamIndex = 0;
                coderOutStreamIndex < folderInfo._coders[coderIndex]._numOutStreams;
                coderOutStreamIndex++, outStreamIndex++
            )
            {
                if (folderInfo.FindBindPairForOutStream(outStreamIndex) < 0)
                {
                    if (foundPrimaryOutStream)
                    {
                        throw new InvalidFormatException("Multiple output streams.");
                    }

                    foundPrimaryOutStream = true;
                    primaryCoderIndex = coderIndex;
                    primaryOutStreamIndex = outStreamIndex;
                }
            }
        }

        if (!foundPrimaryOutStream)
        {
            throw new InvalidFormatException("No output stream.");
        }
    }

    private static Stream CreateDecoderStream(
        Stream[] packStreams,
        long[] packSizes,
        Stream[] outStreams,
        CFolder folderInfo,
        int coderIndex,
        IPasswordProvider pass
    )
    {
        var coderInfo = folderInfo._coders[coderIndex];
        if (coderInfo._numOutStreams != 1)
        {
            throw new NotSupportedException("Multiple output streams are not supported.");
        }

        var inStreamId = 0;
        for (var i = 0; i < coderIndex; i++)
        {
            inStreamId += folderInfo._coders[i]._numInStreams;
        }

        var outStreamId = 0;
        for (var i = 0; i < coderIndex; i++)
        {
            outStreamId += folderInfo._coders[i]._numOutStreams;
        }

        var inStreams = new Stream[coderInfo._numInStreams];

        for (var i = 0; i < inStreams.Length; i++, inStreamId++)
        {
            var bindPairIndex = folderInfo.FindBindPairForInStream(inStreamId);
            if (bindPairIndex >= 0)
            {
                var pairedOutIndex = folderInfo._bindPairs[bindPairIndex]._outIndex;

                if (outStreams[pairedOutIndex] != null)
                {
                    throw new NotSupportedException(
                        "Overlapping stream bindings are not supported."
                    );
                }

                var otherCoderIndex = FindCoderIndexForOutStreamIndex(folderInfo, pairedOutIndex);
                inStreams[i] = CreateDecoderStream(
                    packStreams,
                    packSizes,
                    outStreams,
                    folderInfo,
                    otherCoderIndex,
                    pass
                );

                //inStreamSizes[i] = folderInfo.UnpackSizes[pairedOutIndex];

                if (outStreams[pairedOutIndex] != null)
                {
                    throw new NotSupportedException(
                        "Overlapping stream bindings are not supported."
                    );
                }

                outStreams[pairedOutIndex] = inStreams[i];
            }
            else
            {
                var index = folderInfo.FindPackStreamArrayIndex(inStreamId);
                if (index < 0)
                {
                    throw new NotSupportedException("Could not find input stream binding.");
                }

                inStreams[i] = packStreams[index];

                //inStreamSizes[i] = packSizes[index];
            }
        }

        var unpackSize = folderInfo._unpackSizes[outStreamId];
        return DecoderRegistry.CreateDecoderStream(
            coderInfo._methodId,
            inStreams,
            coderInfo._props,
            pass,
            unpackSize
        );
    }

    private static async ValueTask<Stream> CreateDecoderStreamAsync(
        Stream[] packStreams,
        long[] packSizes,
        Stream[] outStreams,
        CFolder folderInfo,
        int coderIndex,
        IPasswordProvider pass,
        CancellationToken cancellationToken
    )
    {
        var coderInfo = folderInfo._coders[coderIndex];
        if (coderInfo._numOutStreams != 1)
        {
            throw new NotSupportedException("Multiple output streams are not supported.");
        }

        var inStreamId = 0;
        for (var i = 0; i < coderIndex; i++)
        {
            inStreamId += folderInfo._coders[i]._numInStreams;
        }

        var outStreamId = 0;
        for (var i = 0; i < coderIndex; i++)
        {
            outStreamId += folderInfo._coders[i]._numOutStreams;
        }

        var inStreams = new Stream[coderInfo._numInStreams];

        for (var i = 0; i < inStreams.Length; i++, inStreamId++)
        {
            var bindPairIndex = folderInfo.FindBindPairForInStream(inStreamId);
            if (bindPairIndex >= 0)
            {
                var pairedOutIndex = folderInfo._bindPairs[bindPairIndex]._outIndex;

                if (outStreams[pairedOutIndex] != null)
                {
                    throw new NotSupportedException(
                        "Overlapping stream bindings are not supported."
                    );
                }

                var otherCoderIndex = FindCoderIndexForOutStreamIndex(folderInfo, pairedOutIndex);
                inStreams[i] = await CreateDecoderStreamAsync(
                        packStreams,
                        packSizes,
                        outStreams,
                        folderInfo,
                        otherCoderIndex,
                        pass,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                //inStreamSizes[i] = folderInfo.UnpackSizes[pairedOutIndex];

                if (outStreams[pairedOutIndex] != null)
                {
                    throw new NotSupportedException(
                        "Overlapping stream bindings are not supported."
                    );
                }

                outStreams[pairedOutIndex] = inStreams[i];
            }
            else
            {
                var index = folderInfo.FindPackStreamArrayIndex(inStreamId);
                if (index < 0)
                {
                    throw new NotSupportedException("Could not find input stream binding.");
                }

                inStreams[i] = packStreams[index];

                //inStreamSizes[i] = packSizes[index];
            }
        }

        var unpackSize = folderInfo._unpackSizes[outStreamId];
        return await DecoderRegistry
            .CreateDecoderStreamAsync(
                coderInfo._methodId,
                inStreams,
                coderInfo._props,
                pass,
                unpackSize,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal static Stream CreateDecoderStream(
        Stream inStream,
        long startPos,
        long[] packSizes,
        CFolder folderInfo,
        IPasswordProvider pass,
        ArrayPool<byte>? bufferPool = null
    )
    {
        if (!folderInfo.CheckStructure())
        {
            throw new NotSupportedException("Unsupported stream binding structure.");
        }

        var inStreams = new Stream[folderInfo._packStreams.Count];
        var outStreams = new Stream[folderInfo._unpackSizes.Count];
        var encrypted = folderInfo.IsEncrypted();
        try
        {
            for (var j = 0; j < folderInfo._packStreams.Count; j++)
            {
                inStreams[j] = new BufferedSubStream(
                    inStream,
                    startPos,
                    packSizes[j],
                    bufferPool ?? ArrayPool<byte>.Shared
                );
                startPos += packSizes[j];
            }
            FindPrimaryOutStreamIndex(
                folderInfo,
                out var primaryCoderIndex,
                out var primaryOutStreamIndex
            );
            var decoder = CreateDecoderStream(
                inStreams,
                packSizes,
                outStreams,
                folderInfo,
                primaryCoderIndex,
                pass
            );
            return encrypted ? new EncryptedDecoderStream(decoder) : decoder;
        }
        catch (DataErrorException ex) when (encrypted)
        {
            DisposeFailedStreams(outStreams, inStreams);
            throw EncryptedDecoderStream.DecodingException(ex);
        }
        catch
        {
            DisposeFailedStreams(outStreams, inStreams);
            throw;
        }
    }

    internal static async ValueTask<Stream> CreateDecoderStreamAsync(
        Stream inStream,
        long startPos,
        long[] packSizes,
        CFolder folderInfo,
        IPasswordProvider pass,
        CancellationToken cancellationToken,
        ArrayPool<byte>? bufferPool = null
    )
    {
        if (!folderInfo.CheckStructure())
        {
            throw new NotSupportedException("Unsupported stream binding structure.");
        }

        var inStreams = new Stream[folderInfo._packStreams.Count];
        var outStreams = new Stream[folderInfo._unpackSizes.Count];
        var encrypted = folderInfo.IsEncrypted();
        try
        {
            for (var j = 0; j < folderInfo._packStreams.Count; j++)
            {
                inStreams[j] = new BufferedSubStream(
                    inStream,
                    startPos,
                    packSizes[j],
                    bufferPool ?? ArrayPool<byte>.Shared
                );
                startPos += packSizes[j];
            }
            FindPrimaryOutStreamIndex(
                folderInfo,
                out var primaryCoderIndex,
                out var primaryOutStreamIndex
            );
            var decoder = await CreateDecoderStreamAsync(
                    inStreams,
                    packSizes,
                    outStreams,
                    folderInfo,
                    primaryCoderIndex,
                    pass,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return encrypted ? new EncryptedDecoderStream(decoder) : decoder;
        }
        catch (DataErrorException ex) when (encrypted)
        {
            await DisposeFailedStreamsAsync(outStreams, inStreams).ConfigureAwait(false);
            throw EncryptedDecoderStream.DecodingException(ex);
        }
        catch
        {
            await DisposeFailedStreamsAsync(outStreams, inStreams).ConfigureAwait(false);
            throw;
        }
    }

    private static void DisposeFailedStreams(Stream[] outputs, Stream[] inputs)
    {
        var seen = new HashSet<Stream>();
        foreach (var streams in new[] { outputs, inputs })
        {
            foreach (var stream in streams)
            {
                if (stream is null || !seen.Add(stream))
                {
                    continue;
                }
                try
                {
                    stream.Dispose();
                }
                catch (Exception)
                {
                    // Preserve the construction exception and still release the other streams.
                }
            }
        }
    }

    private static async ValueTask DisposeFailedStreamsAsync(Stream[] outputs, Stream[] inputs)
    {
        var seen = new HashSet<Stream>();
        foreach (var streams in new[] { outputs, inputs })
        {
            foreach (var stream in streams)
            {
                if (stream is null || !seen.Add(stream))
                {
                    continue;
                }
                try
                {
                    if (stream is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
#pragma warning disable VSTHRD103 // Legacy streams expose only synchronous disposal.
                        stream.Dispose();
#pragma warning restore VSTHRD103
                    }
                }
                catch (Exception)
                {
                    // Preserve the construction exception and still release the other streams.
                }
            }
        }
    }
}
