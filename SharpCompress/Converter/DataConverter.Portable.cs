using System;

namespace SharpCompress.Converter
{
    // This is a portable version of Mono's DataConverter class with just the small subset of functionality
    //  needed by SharpCompress. Portable in this case means that it contains no unsafe code.
    //
    // This class simply wraps BitConverter and reverses byte arrays when endianess doesn't match the host's.
    //
    // Everything public in this class must match signatures in Mono's DataConverter.

    abstract class DataConverter
    {
        static readonly DataConverter copyConverter = new CopyConverter();
        static readonly DataConverter swapConverter = new SwapConverter();

        static readonly bool isLittleEndian = BitConverter.IsLittleEndian;

        public static DataConverter LittleEndian
        {
            get { return isLittleEndian ? copyConverter : swapConverter; }
        }

        public static DataConverter BigEndian
        {
            get { return isLittleEndian ? swapConverter : copyConverter; }
        }

        public abstract Int16 GetInt16(byte[] data, int index);
        public abstract UInt16 GetUInt16(byte[] data, int index);
        public abstract Int32 GetInt32(byte[] data, int index);
        public abstract UInt32 GetUInt32(byte[] data, int index);
        public abstract Int64 GetInt64(byte[] data, int index);
        public abstract UInt64 GetUInt64(byte[] data, int index);

        public abstract byte[] GetBytes(Int16 value);
        public abstract byte[] GetBytes(UInt16 value);
        public abstract byte[] GetBytes(Int32 value);
        public abstract byte[] GetBytes(UInt32 value);
        public abstract byte[] GetBytes(Int64 value);
        public abstract byte[] GetBytes(UInt64 value);

        public void PutBytes(byte[] data, int index, Int16 value)
        {
            byte[] temp = GetBytes(value);
            Array.Copy(temp, 0, data, index, 2);
        }
        
        public void PutBytes(byte[] data, int index, UInt16 value)
        {
            byte[] temp = GetBytes(value);
            Array.Copy(temp, 0, data, index, 2);
        }

        public void PutBytes(byte[] data, int index, Int32 value)
        {
            byte[] temp = GetBytes(value);
            Array.Copy(temp, 0, data, index, 4);
        }

        public void PutBytes(byte[] data, int index, UInt32 value)
        {
            byte[] temp = GetBytes(value);
            Array.Copy(temp, 0, data, index, 4);
        }

        public void PutBytes(byte[] data, int index, Int64 value)
        {
            byte[] temp = GetBytes(value);
            Array.Copy(temp, 0, data, index, 8);
        }

        public void PutBytes(byte[] data, int index, UInt64 value)
        {
            byte[] temp = GetBytes(value);
            Array.Copy(temp, 0, data, index, 8);
        }

        // CopyConverter wraps BitConverter making all conversions host endian
        class CopyConverter : DataConverter
        {
            public override Int16 GetInt16(byte[] data, int index)
            {
                return BitConverter.ToInt16(data, index);
            }

            public override UInt16 GetUInt16(byte[] data, int index)
            {
                return BitConverter.ToUInt16(data, index);
            }

            public override Int32 GetInt32(byte[] data, int index)
            {
                return BitConverter.ToInt32(data, index);
            }

            public override UInt32 GetUInt32(byte[] data, int index)
            {
                return BitConverter.ToUInt32(data, index);
            }

            public override Int64 GetInt64(byte[] data, int index)
            {
                return BitConverter.ToInt64(data, index);
            }

            public override UInt64 GetUInt64(byte[] data, int index)
            {
                return BitConverter.ToUInt64(data, index);
            }

            public override byte[] GetBytes(Int16 value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(UInt16 value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(Int32 value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(UInt32 value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(Int64 value)
            {
                return BitConverter.GetBytes(value);
            }

            public override byte[] GetBytes(UInt64 value)
            {
                return BitConverter.GetBytes(value);
            }
        }

        // SwapConverter wraps and reverses BitConverter making all conversions the opposite of host endian
        class SwapConverter : DataConverter
        {
            public override Int16 GetInt16(byte[] data, int index)
            {
                byte[] temp = new byte[2];
                Array.Copy(data, index, temp, 0, 2);
                Array.Reverse(temp);
                return BitConverter.ToInt16(temp, 0);
            }

            public override UInt16 GetUInt16(byte[] data, int index)
            {
                byte[] temp = new byte[2];
                Array.Copy(data, index, temp, 0, 2);
                Array.Reverse(temp);
                return BitConverter.ToUInt16(temp, 0);
            }

            public override Int32 GetInt32(byte[] data, int index)
            {
                byte[] temp = new byte[4];
                Array.Copy(data, index, temp, 0, 4);
                Array.Reverse(temp);
                return BitConverter.ToInt32(temp, 0);
            }

            public override UInt32 GetUInt32(byte[] data, int index)
            {
                byte[] temp = new byte[4];
                Array.Copy(data, index, temp, 0, 4);
                Array.Reverse(temp);
                return BitConverter.ToUInt32(temp, 0);
            }

            public override Int64 GetInt64(byte[] data, int index)
            {
                byte[] temp = new byte[8];
                Array.Copy(data, index, temp, 0, 8);
                Array.Reverse(temp);
                return BitConverter.ToInt64(temp, 0);
            }

            public override UInt64 GetUInt64(byte[] data, int index)
            {
                byte[] temp = new byte[8];
                Array.Copy(data, index, temp, 0, 8);
                Array.Reverse(temp);
                return BitConverter.ToUInt64(temp, 0);
            }

            public override byte[] GetBytes(Int16 value)
            {
                byte[] ret = BitConverter.GetBytes(value);
                Array.Reverse(ret);
                return ret;
            }

            public override byte[] GetBytes(UInt16 value)
            {
                byte[] ret = BitConverter.GetBytes(value);
                Array.Reverse(ret);
                return ret;
            }

            public override byte[] GetBytes(Int32 value)
            {
                byte[] ret = BitConverter.GetBytes(value);
                Array.Reverse(ret);
                return ret;
            }

            public override byte[] GetBytes(UInt32 value)
            {
                byte[] ret = BitConverter.GetBytes(value);
                Array.Reverse(ret);
                return ret;
            }

            public override byte[] GetBytes(Int64 value)
            {
                byte[] ret = BitConverter.GetBytes(value);
                Array.Reverse(ret);
                return ret;
            }

            public override byte[] GetBytes(UInt64 value)
            {
                byte[] ret = BitConverter.GetBytes(value);
                Array.Reverse(ret);
                return ret;
            }
        }
    }
}