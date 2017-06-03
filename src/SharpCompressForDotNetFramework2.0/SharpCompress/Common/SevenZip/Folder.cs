using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Compressor;
using SharpCompress.Compressor.BZip2;
using SharpCompress.Compressor.Filters;
using SharpCompress.Compressor.LZMA;
using SharpCompress.Compressor.PPMd;

namespace SharpCompress.Common.SevenZip
{
    internal class Folder
    {
        public CodersInfo[] Coders;
        public BindPair[] BindPairs;
        public ulong[] PackedStreamIndices;
        public ulong[] UnpackedStreamSizes;
        public uint? UnpackCRC;
        public PackedStreamInfo[] PackedStreams;
        public UnpackedStreamInfo[] UnpackedStreams;

        private SevenZipHeaderFactory factory;

        internal Folder(SevenZipHeaderFactory factory)
        {
            this.factory = factory;
        }

        public ulong GetUnpackSize()
        {
            int outStreams = Coders.Aggregate(0, (sum, coder) => sum + (int)coder.NumberOfOutStreams);
            for (int j = 0; j < outStreams; j++)
            {
                if (!BindPairs.Where(x => x.OutIndex == (ulong)j).Any())
                {
                    return UnpackedStreamSizes[j];
                }
            }
            return 0;
        }

        internal IEnumerable<SevenZipCompressionType> GetCompressions()
        {
            foreach (var coder in Coders)
            {
                var type = new SevenZipCompressionType();
                type.Coder = coder;
                if (coder.Method.Length == 3 && coder.Method[0] == 3 && coder.Method[1] == 1 && coder.Method[2] == 1)
                {
                    type.CompressionType = CompressionType.LZMA;
                }
                else if (coder.Method.Length == 1 && coder.Method[0] == 33)
                {
                    type.CompressionType = CompressionType.LZMA;
                }
                else if (coder.Method.Length == 3 && coder.Method[0] == 3 && coder.Method[1] == 4 && coder.Method[2] == 1)
                {
                    type.CompressionType = CompressionType.PPMd;
                }
                else if (coder.Method.Length == 3 && coder.Method[0] == 4 && coder.Method[1] == 2 && coder.Method[2] == 2)
                {
                    type.CompressionType = CompressionType.BZip2;
                }
                else if (coder.Method.Length == 4 && coder.Method[0] == 3 && coder.Method[1] == 3 && coder.Method[2] == 1 && coder.Method[3] == 3)
                {
                    type.CompressionType = CompressionType.BCJ;
                }
                else if (coder.Method.Length == 4 && coder.Method[0] == 3 && coder.Method[1] == 3 && coder.Method[2] == 1 && coder.Method[3] == 0x1B)
                {
                    type.CompressionType = CompressionType.BCJ2;
                }
                yield return type;
            }
        }

        internal Stream GetStream()
        {
            factory.BaseStream.Seek(factory.BaseOffset + (long)PackedStreams[0].StartPosition, SeekOrigin.Begin);
            Stream stream = null;
            foreach (var type in GetCompressions())
            {
                switch (type.CompressionType)
                {
                    case CompressionType.LZMA:
                        {
                            if (type.Coder.Method.Length == 3
                                && type.Coder.Method[0] == 3
                                && type.Coder.Method[1] == 1
                                && type.Coder.Method[2] == 1)
                            {
                                stream = new LzmaStream(type.Coder.Properties, factory.BaseStream, (long)PackedStreams[0].PackedSize, (long)UnpackedStreamSizes[0]);
                            }
                            else if (type.Coder.Method.Length == 1
                                && type.Coder.Method[0] == 33)
                            {
                                stream = new LzmaStream(type.Coder.Properties, factory.BaseStream, (long)PackedStreams[0].PackedSize, (long)UnpackedStreamSizes[0]);
                            }
                        }
                        break;
                    case CompressionType.PPMd:
                        {
                            stream = new PpmdStream(new PpmdProperties(type.Coder.Properties), factory.BaseStream, false);
                        }
                        break;
                    case CompressionType.BZip2:
                        {
                            stream = new BZip2Stream(factory.BaseStream, CompressionMode.Decompress, true);
                        }
                        break;
                    case CompressionType.BCJ:
                        {
                            stream = new BCJFilter(false, stream);
                        }
                        break;
                    case CompressionType.BCJ2:
                        {
                            long pos = factory.BaseStream.Position;

                            byte[] data1 = new byte[PackedStreams[1].PackedSize];
                            factory.BaseStream.Seek(factory.BaseOffset + (long)PackedStreams[1].StartPosition, SeekOrigin.Begin);
                            factory.BaseStream.Read(data1, 0, data1.Length);
                            byte[] data2 = new byte[PackedStreams[2].PackedSize];
                            factory.BaseStream.Seek(factory.BaseOffset + (long)PackedStreams[2].StartPosition, SeekOrigin.Begin);
                            factory.BaseStream.Read(data2, 0, data2.Length);
                            byte[] control = new byte[PackedStreams[3].PackedSize];
                            factory.BaseStream.Seek(factory.BaseOffset + (long)PackedStreams[3].StartPosition, SeekOrigin.Begin);
                            factory.BaseStream.Read(control, 0, control.Length);

                            factory.BaseStream.Seek(pos, SeekOrigin.Begin);
                            stream = new BCJ2Filter(control, data1, data2, stream);
                        }
                        break;
                    default:
                        {
                            throw new NotSupportedException("Unknown coder.");
                        }
                }
            }
            return stream;
        }
    }
    internal class CodersInfo
    {
        public byte[] Method;
        public ulong NumberOfInStreams;
        public ulong NumberOfOutStreams;
        public byte[] Properties;
    }

    internal class BindPair
    {
        public ulong InIndex;
        public ulong OutIndex;
    }
    internal class SevenZipCompressionType
    {
        internal CompressionType CompressionType { get; set; }
        internal CodersInfo Coder { get; set; }
    }
}
