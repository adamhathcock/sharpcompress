using System;
using System.IO;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA
{
    internal abstract class DecoderStream2 : Stream
    {
        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return false; } }

        public override bool CanWrite { get { return false; } }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length { get { throw new NotSupportedException(); } }

        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    internal static class DecoderStreamHelper
    {
        private static int FindCoderIndexForOutStreamIndex(CFolder folderInfo, int outStreamIndex)
        {
            for (int coderIndex = 0; coderIndex < folderInfo.Coders.Count; coderIndex++)
            {
                var coderInfo = folderInfo.Coders[coderIndex];
                outStreamIndex -= coderInfo.NumOutStreams;
                if (outStreamIndex < 0)
                {
                    return coderIndex;
                }
            }

            throw new InvalidOperationException("Could not link output stream to coder.");
        }

        private static void FindPrimaryOutStreamIndex(CFolder folderInfo, out int primaryCoderIndex,
                                                      out int primaryOutStreamIndex)
        {
            bool foundPrimaryOutStream = false;
            primaryCoderIndex = -1;
            primaryOutStreamIndex = -1;

            for (int outStreamIndex = 0, coderIndex = 0;
                 coderIndex < folderInfo.Coders.Count;
                 coderIndex++)
            {
                for (int coderOutStreamIndex = 0;
                     coderOutStreamIndex < folderInfo.Coders[coderIndex].NumOutStreams;
                     coderOutStreamIndex++, outStreamIndex++)
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

        private static Stream CreateDecoderStream(Stream[] packStreams, long[] packSizes, Stream[] outStreams,
                                                  CFolder folderInfo, int coderIndex, IPasswordProvider pass)
        {
            var coderInfo = folderInfo.Coders[coderIndex];
            if (coderInfo.NumOutStreams != 1)
            {
                throw new NotSupportedException("Multiple output streams are not supported.");
            }

            int inStreamId = 0;
            for (int i = 0; i < coderIndex; i++)
            {
                inStreamId += folderInfo.Coders[i].NumInStreams;
            }

            int outStreamId = 0;
            for (int i = 0; i < coderIndex; i++)
            {
                outStreamId += folderInfo.Coders[i].NumOutStreams;
            }

            Stream[] inStreams = new Stream[coderInfo.NumInStreams];

            for (int i = 0; i < inStreams.Length; i++, inStreamId++)
            {
                int bindPairIndex = folderInfo.FindBindPairForInStream(inStreamId);
                if (bindPairIndex >= 0)
                {
                    int pairedOutIndex = folderInfo.BindPairs[bindPairIndex].OutIndex;

                    if (outStreams[pairedOutIndex] != null)
                    {
                        throw new NotSupportedException("Overlapping stream bindings are not supported.");
                    }

                    int otherCoderIndex = FindCoderIndexForOutStreamIndex(folderInfo, pairedOutIndex);
                    inStreams[i] = CreateDecoderStream(packStreams, packSizes, outStreams, folderInfo, otherCoderIndex,
                                                       pass);

                    //inStreamSizes[i] = folderInfo.UnpackSizes[pairedOutIndex];

                    if (outStreams[pairedOutIndex] != null)
                    {
                        throw new NotSupportedException("Overlapping stream bindings are not supported.");
                    }

                    outStreams[pairedOutIndex] = inStreams[i];
                }
                else
                {
                    int index = folderInfo.FindPackStreamArrayIndex(inStreamId);
                    if (index < 0)
                    {
                        throw new NotSupportedException("Could not find input stream binding.");
                    }

                    inStreams[i] = packStreams[index];

                    //inStreamSizes[i] = packSizes[index];
                }
            }

            long unpackSize = folderInfo.UnpackSizes[outStreamId];
            return DecoderRegistry.CreateDecoderStream(coderInfo.MethodId, inStreams, coderInfo.Props, pass, unpackSize);
        }

        internal static Stream CreateDecoderStream(Stream inStream, long startPos, long[] packSizes, CFolder folderInfo,
                                                   IPasswordProvider pass)
        {
            if (!folderInfo.CheckStructure())
            {
                throw new NotSupportedException("Unsupported stream binding structure.");
            }

            Stream[] inStreams = new Stream[folderInfo.PackStreams.Count];
            for (int j = 0; j < folderInfo.PackStreams.Count; j++)
            {
                inStreams[j] = new BufferedSubStream(inStream, startPos, packSizes[j]);
                startPos += packSizes[j];
            }

            Stream[] outStreams = new Stream[folderInfo.UnpackSizes.Count];

            int primaryCoderIndex, primaryOutStreamIndex;
            FindPrimaryOutStreamIndex(folderInfo, out primaryCoderIndex, out primaryOutStreamIndex);
            return CreateDecoderStream(inStreams, packSizes, outStreams, folderInfo, primaryCoderIndex, pass);
        }
    }
}