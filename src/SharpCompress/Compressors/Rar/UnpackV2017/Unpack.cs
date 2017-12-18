#if RARWIP
using System;
using System.IO;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar.UnpackV2017
{
    internal partial class Unpack : IRarUnpack
    {
        private FileHeader fileHeader;
        private Stream readStream;
        private Stream writeStream;

        public void DoUnpack(FileHeader fileHeader, Stream readStream, Stream writeStream)
        {
            // may be long.MaxValue which could indicate unknown size (not present in header)
            DestUnpSize = fileHeader.UncompressedSize;
            this.fileHeader = fileHeader;
            this.readStream = readStream;
            this.writeStream = writeStream;
//            if (!fileHeader.IsSolid)
//            {
//                Init(null);
//            }
fileHeader.
            Init(size_t WinSize, fileHeader.IsSolid)
            Suspended = false;
            DoUnpack();
        }

        public void DoUnpack()
        {
            if (this.fileHeader.CompressionMethod == 0)
            {
                UnstoreFile();
            } else {
                DoUnpack(uint Method,bool Solid);
            }
        }

        private void UnstoreFile()
        {
            var b = new byte[0x10000];
            while (true)
            {
                int n = this.readStream.Read(b, 0, (int)Math.Min(b.Length, DestUnpSize));
                if (n == 0)
                {
                    break;
                }

                n = n < DestUnpSize ? n : (int)DestUnpSize;
                this.writeStream.Write(b, 0, n);
                if (DestUnpSize >= 0)
                {
                    DestUnpSize -= n;
                }
                if (Suspended)
                {
                    return;
                }
            }
        }
        
        public bool Suspended { get; set; }

        public long DestSize { get => DestUnpSize; }

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

        public int PpmEscChar { get => this.PPMEscChar; set => this.PPMEscChar = value; } 
    }
}
#endif