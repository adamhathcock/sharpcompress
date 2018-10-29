//
// Authors:
//   Miguel de Icaza (miguel@novell.com)
//
// See the following url for documentation:
//     http://www.mono-project.com/Mono_DataConvert
//
// Compilation Options:
//     MONO_DATACONVERTER_PUBLIC:
//         Makes the class public instead of the default internal.
//
//     MONO_DATACONVERTER_STATIC_METHODS:     
//         Exposes the public static methods.
//
// TODO:
//   Support for "DoubleWordsAreSwapped" for ARM devices
//
// Copyright (C) 2006 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;

#pragma warning disable 3021

namespace SharpCompress.Converters
{
    internal abstract class DataConverter
    {
        // Disables the warning: CLS compliance checking will not be performed on
        //  `XXXX' because it is not visible from outside this assembly
#pragma warning disable 3019
        private static readonly DataConverter SwapConv = new SwapConverter();

        public static readonly bool IsLittleEndian = BitConverter.IsLittleEndian;

        public abstract double GetDouble(byte[] data, int index);
        public abstract float GetFloat(byte[] data, int index);
        public abstract long GetInt64(byte[] data, int index);
        public abstract int GetInt32(byte[] data, int index);

        public abstract short GetInt16(byte[] data, int index);

        [CLSCompliant(false)]
        public abstract uint GetUInt32(byte[] data, int index);

        [CLSCompliant(false)]
        public abstract ushort GetUInt16(byte[] data, int index);

        [CLSCompliant(false)]
        public abstract ulong GetUInt64(byte[] data, int index);

        public abstract void PutBytes(byte[] dest, int destIdx, double value);
        public abstract void PutBytes(byte[] dest, int destIdx, float value);
        public abstract void PutBytes(byte[] dest, int destIdx, int value);
        public abstract void PutBytes(byte[] dest, int destIdx, long value);
        public abstract void PutBytes(byte[] dest, int destIdx, short value);

        [CLSCompliant(false)]
        public abstract void PutBytes(byte[] dest, int destIdx, ushort value);

        [CLSCompliant(false)]
        public abstract void PutBytes(byte[] dest, int destIdx, uint value);

        [CLSCompliant(false)]
        public abstract void PutBytes(byte[] dest, int destIdx, ulong value);

#if NOTNEEDED
        public byte[] GetBytes(double value)
        {
            byte[] ret = new byte[8];
            PutBytes(ret, 0, value);
            return ret;
        }

        public byte[] GetBytes(float value)
        {
            byte[] ret = new byte[4];
            PutBytes(ret, 0, value);
            return ret;
        }
#endif

        public byte[] GetBytes(int value)
        {
            byte[] ret = new byte[4];
            PutBytes(ret, 0, value);
            return ret;
        }

        public byte[] GetBytes(long value)
        {
            byte[] ret = new byte[8];
            PutBytes(ret, 0, value);
            return ret;
        }

#if NOTNEEDED
        public byte[] GetBytes(short value)
        {
            byte[] ret = new byte[2];
            PutBytes(ret, 0, value);
            return ret;
        }
#endif

        [CLSCompliant(false)]
        public byte[] GetBytes(ushort value)
        {
            byte[] ret = new byte[2];
            PutBytes(ret, 0, value);
            return ret;
        }

        [CLSCompliant(false)]
        public byte[] GetBytes(uint value)
        {
            byte[] ret = new byte[4];
            PutBytes(ret, 0, value);
            return ret;
        }

        [CLSCompliant(false)]
        public byte[] GetBytes(ulong value)
        {
            byte[] ret = new byte[8];
            PutBytes(ret, 0, value);
            return ret;
        }

        static public DataConverter LittleEndian => BitConverter.IsLittleEndian ? Native : SwapConv;

#if NOTNEEDED
        static public DataConverter BigEndian => BitConverter.IsLittleEndian ? SwapConv : Native;
#endif

        static public DataConverter Native { get; } = new CopyConverter();

        internal void Check(byte[] dest, int destIdx, int size)
        {
            if (dest == null)
            {
                throw new ArgumentNullException(nameof(dest));
            }
            if (destIdx < 0 || destIdx > dest.Length - size)
            {
                throw new ArgumentException("destIdx");
            }
        }

        private class CopyConverter : DataConverter
        {
            public override double GetDouble(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 8)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToDouble(data, index);
            }

            public override ulong GetUInt64(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 8)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }
                
                return BitConverter.ToUInt64(data, index);
            }

            public override long GetInt64(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 8)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToInt64(data, index);
            }

            public override float GetFloat(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 4)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToSingle(data, index);
            }

            public override int GetInt32(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 4)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToInt32(data, index);
            }

            public override uint GetUInt32(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 4)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToUInt32(data, index);
            }

            public override short GetInt16(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 2)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToInt16(data, index);
            }

            public override ushort GetUInt16(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 2)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToUInt16(data, index);
            }

            public override void PutBytes(byte[] dest, int destIdx, double value)
            {
                Check(dest, destIdx, 8);
                BitConverter.GetBytes(value).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, float value)
            {
                Check(dest, destIdx, 4);
                BitConverter.GetBytes(value).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, int value)
            {
                Check(dest, destIdx, 4);
                BitConverter.GetBytes(value).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, uint value)
            {
                Check(dest, destIdx, 4);
                BitConverter.GetBytes(value).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, long value)
            {
                Check(dest, destIdx, 8);
                BitConverter.GetBytes(value).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, ulong value)
            {
                Check(dest, destIdx, 8);
                BitConverter.GetBytes(value).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, short value)
            {
                Check(dest, destIdx, 2);
                BitConverter.GetBytes(value).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, ushort value)
            {
                Check(dest, destIdx, 2);
                BitConverter.GetBytes(value).CopyTo(dest, destIdx);
            }
        }

        private class SwapConverter : DataConverter
        {
            private Byte[] SwapBytes(byte[] data)
            {
                return SwapBytes(data, 0, data.Length);
            }

            private Byte[] SwapBytes(byte[] data, int index, int length)
            {
                var b = new Byte[length];
                for (int i = 0; i < length; i++)
                    b[length - i - 1] = data[index + i];
                return b;
            }

            public override double GetDouble(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 8)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToDouble(SwapBytes(data, index, 8), 0);
            }

            public override ulong GetUInt64(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 8)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToUInt64(SwapBytes(data, index, 8), 0);
            }

            public override long GetInt64(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 8)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToInt64(SwapBytes(data, index, 8), 0);
            }

            public override float GetFloat(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 4)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToSingle(SwapBytes(data, index, 4), 0);
            }

            public override int GetInt32(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 4)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToInt32(SwapBytes(data, index, 4), 0);
            }

            public override uint GetUInt32(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 4)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToUInt32(SwapBytes(data, index, 4), 0);
            }

            public override short GetInt16(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 2)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToInt16(SwapBytes(data, index, 2), 0);
            }

            public override ushort GetUInt16(byte[] data, int index)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (data.Length - index < 2)
                {
                    throw new ArgumentException("index");
                }
                if (index < 0)
                {
                    throw new ArgumentException("index");
                }

                return BitConverter.ToUInt16(SwapBytes(data, index, 2), 0);
            }

            public override void PutBytes(byte[] dest, int destIdx, double value)
            {
                Check(dest, destIdx, 8);
                SwapBytes(BitConverter.GetBytes(value)).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, float value)
            {
                Check(dest, destIdx, 4);
                SwapBytes(BitConverter.GetBytes(value)).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, int value)
            {
                Check(dest, destIdx, 4);
                SwapBytes(BitConverter.GetBytes(value)).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, uint value)
            {
                Check(dest, destIdx, 4);
                SwapBytes(BitConverter.GetBytes(value)).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, long value)
            {
                Check(dest, destIdx, 8);
                SwapBytes(BitConverter.GetBytes(value)).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, ulong value)
            {
                Check(dest, destIdx, 8);
                SwapBytes(BitConverter.GetBytes(value)).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, short value)
            {
                Check(dest, destIdx, 2);
                SwapBytes(BitConverter.GetBytes(value)).CopyTo(dest, destIdx);
            }

            public override void PutBytes(byte[] dest, int destIdx, ushort value)
            {
                Check(dest, destIdx, 2);
                SwapBytes(BitConverter.GetBytes(value)).CopyTo(dest, destIdx);
            }
        }
        
    }
}
