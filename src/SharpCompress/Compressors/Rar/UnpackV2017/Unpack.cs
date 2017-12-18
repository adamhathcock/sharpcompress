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
            DestUnpSize = fileHeader.UncompressedSize;
            this.fileHeader = fileHeader;
            this.readStream = readStream;
            this.writeStream = writeStream;
            if (!fileHeader.IsSolid)
            {
                Init(null);
                Init(size_t WinSize,bool Solid)
            }
            Suspended = false;
            DoUnpack();
        }

        public void DoUnpack()
        {
            if (this.fileHeader.CompressionMethod == 0)
            {
                UnstoreFile();
                return;
            }
            DoUnpack(uint Method,bool Solid);
        }

        public bool Suspended { get; set; }

        public long DestSize { get => DestUnpSize; }

        public int Char {
            get { throw new NotImplementedException(); }
        }

        public int PpmEscChar {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
    }
}
#endif