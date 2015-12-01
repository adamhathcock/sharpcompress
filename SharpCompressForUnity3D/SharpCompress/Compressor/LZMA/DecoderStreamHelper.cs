namespace SharpCompress.Compressor.LZMA
{
    using SharpCompress.Common.SevenZip;
    using SharpCompress.Compressor.LZMA.Utilites;
    using SharpCompress.IO;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    internal static class DecoderStreamHelper
    {
        internal static Stream CreateDecoderStream(Stream inStream, long startPos, long[] packSizes, CFolder folderInfo, IPasswordProvider pass)
        {
            int num2;
            int num3;
            if (!folderInfo.CheckStructure())
            {
                throw new NotSupportedException("Unsupported stream binding structure.");
            }
            Stream[] packStreams = new Stream[folderInfo.PackStreams.Count];
            for (int i = 0; i < folderInfo.PackStreams.Count; i++)
            {
                packStreams[i] = new ReadOnlySubStream(inStream, new long?(startPos), packSizes[i]);
                startPos += packSizes[i];
            }
            Stream[] outStreams = new Stream[folderInfo.UnpackSizes.Count];
            FindPrimaryOutStreamIndex(folderInfo, out num2, out num3);
            return CreateDecoderStream(packStreams, packSizes, outStreams, folderInfo, num2, pass);
        }

        private static Stream CreateDecoderStream(Stream[] packStreams, long[] packSizes, Stream[] outStreams, CFolder folderInfo, int coderIndex, IPasswordProvider pass)
        {
            int num2;
            CCoderInfo info = folderInfo.Coders[coderIndex];
            if (info.NumOutStreams != 1)
            {
                throw new NotSupportedException("Multiple output streams are not supported.");
            }
            int inStreamIndex = 0;
            for (num2 = 0; num2 < coderIndex; num2++)
            {
                inStreamIndex += folderInfo.Coders[num2].NumInStreams;
            }
            int num3 = 0;
            for (num2 = 0; num2 < coderIndex; num2++)
            {
                num3 += folderInfo.Coders[num2].NumOutStreams;
            }
            Stream[] inStreams = new Stream[info.NumInStreams];
            num2 = 0;
            while (num2 < inStreams.Length)
            {
                int num4 = folderInfo.FindBindPairForInStream(inStreamIndex);
                if (num4 >= 0)
                {
                    int outIndex = folderInfo.BindPairs[num4].OutIndex;
                    if (outStreams[outIndex] != null)
                    {
                        throw new NotSupportedException("Overlapping stream bindings are not supported.");
                    }
                    int num6 = FindCoderIndexForOutStreamIndex(folderInfo, outIndex);
                    inStreams[num2] = CreateDecoderStream(packStreams, packSizes, outStreams, folderInfo, num6, pass);
                    if (outStreams[outIndex] != null)
                    {
                        throw new NotSupportedException("Overlapping stream bindings are not supported.");
                    }
                    outStreams[outIndex] = inStreams[num2];
                }
                else
                {
                    int index = folderInfo.FindPackStreamArrayIndex(inStreamIndex);
                    if (index < 0)
                    {
                        throw new NotSupportedException("Could not find input stream binding.");
                    }
                    inStreams[num2] = packStreams[index];
                }
                num2++;
                inStreamIndex++;
            }
            long limit = folderInfo.UnpackSizes[num3];
            return DecoderRegistry.CreateDecoderStream(info.MethodId, inStreams, info.Props, pass, limit);
        }

        private static int FindCoderIndexForOutStreamIndex(CFolder folderInfo, int outStreamIndex)
        {
            for (int i = 0; i < folderInfo.Coders.Count; i++)
            {
                CCoderInfo info = folderInfo.Coders[i];
                outStreamIndex -= info.NumOutStreams;
                if (outStreamIndex < 0)
                {
                    return i;
                }
            }
            throw new InvalidOperationException("Could not link output stream to coder.");
        }

        private static void FindPrimaryOutStreamIndex(CFolder folderInfo, out int primaryCoderIndex, out int primaryOutStreamIndex)
        {
            bool flag = false;
            primaryCoderIndex = -1;
            primaryOutStreamIndex = -1;
            int outStreamIndex = 0;
            for (int i = 0; i < folderInfo.Coders.Count; i++)
            {
                int num3 = 0;
                while (num3 < folderInfo.Coders[i].NumOutStreams)
                {
                    if (folderInfo.FindBindPairForOutStream(outStreamIndex) < 0)
                    {
                        if (flag)
                        {
                            throw new NotSupportedException("Multiple output streams.");
                        }
                        flag = true;
                        primaryCoderIndex = i;
                        primaryOutStreamIndex = outStreamIndex;
                    }
                    num3++;
                    outStreamIndex++;
                }
            }
            if (!flag)
            {
                throw new NotSupportedException("No output stream.");
            }
        }
    }
}

