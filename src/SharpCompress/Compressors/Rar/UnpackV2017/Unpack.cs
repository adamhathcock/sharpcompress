#if RARWIP
#if !Rar2017_64bit
using nint = System.Int32;
using nuint = System.UInt32;
using size_t = System.UInt32;
#else
using nint = System.Int64;
using nuint = System.UInt64;
using size_t = System.UInt64;
#endif
using int64 = System.Int64;

using System;
using System.IO;
using SharpCompress.Common.Rar.Headers;
using static SharpCompress.Compressors.Rar.UnpackV2017.PackDef;

namespace SharpCompress.Compressors.Rar.UnpackV2017
{
    internal partial class Unpack : IRarUnpack
    {
        private FileHeader fileHeader;
        private Stream readStream;
        private Stream writeStream;

        private void _UnpackCtor() {
            BlockTables.Init();
            for (int i = 0; i < this.AudV.Length; i++) {
                this.AudV[i] = new AudioVariables();
            }
        }

        private int UnpIO_UnpRead(byte[] buf, int offset, int count) {
            // NOTE: caller has logic to check for -1 for error we throw instead.
            return this.readStream.Read(buf, offset, count);
        }

        private void UnpIO_UnpWrite(byte[] buf, size_t offset, uint count) {
            this.writeStream.Write(buf, checked((int)offset), checked((int)count));
        }

        public void DoUnpack(FileHeader fileHeader, Stream readStream, Stream writeStream)
        {
            // as of 12/2017 .NET limits array indexing to using a signed integer
            // MaxWinSize causes unpack to use a fragmented window when the file 
            // window size exceeds MaxWinSize
            MaxWinSize = ((uint)int.MaxValue) + 1;

            // may be long.MaxValue which could indicate unknown size (not present in header)
            DestUnpSize = fileHeader.UncompressedSize;
            this.fileHeader = fileHeader;
            this.readStream = readStream;
            this.writeStream = writeStream;
            if (!fileHeader.IsStored) {
                Init(fileHeader.WindowSize, fileHeader.IsSolid);
            }
            Suspended = false;
            DoUnpack();
        }

        public void DoUnpack()
        {
            if (this.fileHeader.IsStored)
            {
                UnstoreFile();
            } else {
                DoUnpack(this.fileHeader.CompressionMethod, this.fileHeader.IsSolid);
            }
        }

        private void UnstoreFile()
        {
            var b = new byte[0x10000];
            do {
                int n = this.readStream.Read(b, 0, (int)Math.Min(b.Length, DestUnpSize));
                if (n == 0)
                {
                    break;
                }
                this.writeStream.Write(b, 0, n);
                DestUnpSize -= n;
            } while (!Suspended);
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

        public static byte[] EnsureCapacity(byte[] array, int length) {
            return array.Length < length ? new byte[length] : array;
        }

    }
}
#endif