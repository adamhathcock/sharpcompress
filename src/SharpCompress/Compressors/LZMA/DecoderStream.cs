using System;
using System.IO;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA.Utilites;
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

        throw new InvalidOperationException("Could not link output stream to coder.");
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
                        throw new NotSupportedException("Multiple output streams.");
                    }

                    foundPrimaryOutStream = true;
                    primaryCoderIndex = coderIndex;
                    primaryOutStreamIndex = outStreamIndex;
                }
            }
        }

        if (!foundPrimaryOutStream)
        {
            throw new NotSupportedException("No output stream.");
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

    internal static Stream CreateDecoderStream(
        Stream inStream,
        long startPos,
        long[] packSizes,
        CFolder folderInfo,
        IPasswordProvider pass
    )
    {
        if (!folderInfo.CheckStructure())
        {
            throw new NotSupportedException("Unsupported stream binding structure.");
        }

        var inStreams = new Stream[folderInfo._packStreams.Count];
        for (var j = 0; j < folderInfo._packStreams.Count; j++)
        {
            inStreams[j] = new BufferedSubStream(inStream, startPos, packSizes[j]);
            startPos += packSizes[j];
        }

        var outStreams = new Stream[folderInfo._unpackSizes.Count];

        FindPrimaryOutStreamIndex(
            folderInfo,
            out var primaryCoderIndex,
            out var primaryOutStreamIndex
        );
        return CreateDecoderStream(
            inStreams,
            packSizes,
            outStreams,
            folderInfo,
            primaryCoderIndex,
            pass
        );
    }
}
