namespace SharpCompress.Common.SevenZip
{
    using SharpCompress.Compressor.LZMA;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct CStreamSwitch : IDisposable
    {
        private ArchiveReader _archive;
        private bool _needRemove;
        private bool _active;
        public void Dispose()
        {
            if (this._active)
            {
                this._active = false;
                Log.WriteLine("[end of switch]");
            }
            if (this._needRemove)
            {
                this._needRemove = false;
                this._archive.DeleteByteStream();
            }
        }

        public void Set(ArchiveReader archive, byte[] dataVector)
        {
            this.Dispose();
            this._archive = archive;
            this._archive.AddByteStream(dataVector, 0, dataVector.Length);
            this._needRemove = true;
            this._active = true;
        }

        public void Set(ArchiveReader archive, List<byte[]> dataVector)
        {
            this.Dispose();
            this._active = true;
            if (archive.ReadByte() != 0)
            {
                int num2 = archive.ReadNum();
                if ((num2 < 0) || (num2 >= dataVector.Count))
                {
                    throw new InvalidOperationException();
                }
                Log.WriteLine("[switch to stream {0}]", new object[] { num2 });
                this._archive = archive;
                this._archive.AddByteStream(dataVector[num2], 0, dataVector[num2].Length);
                this._needRemove = true;
                this._active = true;
            }
            else
            {
                Log.WriteLine("[inline data]");
            }
        }
    }
}

