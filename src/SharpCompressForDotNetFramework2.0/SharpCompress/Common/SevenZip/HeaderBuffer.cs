using System;
using System.Text;

namespace SharpCompress.Common.SevenZip
{
    internal class HeaderBuffer
    {
        public byte[] Bytes { get; set; }
        public int Offset { get; set; }

        public HeaderProperty ReadProperty()
        {
            return (HeaderProperty)ReadByte();
        }

        public T[] CreateArray<T>()
            where T : new()
        {
            int count = (int)ReadEncodedInt64();
            var array = new T[count];
            for (int i = 0; i < count; i++)
            {
                array[i] = new T();
            }
            return array;
        }

        public byte ReadByte()
        {
            return Bytes[Offset++];
        }

        public string ReadName()
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = Offset; i < Bytes.Length; i += 2)
            {
                char c = BitConverter.ToChar(Bytes, i);
                if (c == 0)
                {
                    Offset = i + 2;
                    return stringBuilder.ToString();
                }
                stringBuilder.Append(c);
            }
            throw new InvalidFormatException("Bad name bytes");
        }

        public byte[] ReadBytes(int count)
        {
            byte[] seg = new byte[count];
            System.Buffer.BlockCopy(Bytes, Offset, seg, 0, count);
            Offset += count;
            return seg;
        }
        public bool[] ReadBoolFlags(int numItems)
        {
            byte b = 0;
            byte mask = 0;

            bool[] flags = new bool[numItems];
            for (int i = 0; i < numItems; i++)
            {
                if (mask == 0)
                {
                    b = ReadByte();
                    mask = 0x80;
                }
                if ((b & mask) != 0)
                {
                    flags[i] = true;
                }
                else
                {
                    flags[i] = false;
                }
                mask >>= 1;
            }
            return flags;
        }

        public bool[] ReadBoolFlagsDefaultTrue(int numItems)
        {
            byte allAreDefined = ReadByte();
            if (allAreDefined == 0)
            {
                return ReadBoolFlags(numItems);
            }
            bool[] flags = new bool[numItems];
            for (int i = 0; i < numItems; i++)
            {
                flags[i] = true;
            }
            return flags;
        }

        public ulong ReadEncodedInt64()
        {
            byte firstByte;
            byte mask = 0x80;
            int i;
            firstByte = Bytes[Offset];
            Offset++;
            ulong value = 0;
            for (i = 0; i < 8; i++)
            {
                byte b;
                if ((firstByte & mask) == 0)
                {
                    ulong highPart = (ulong)(firstByte & (mask - 1));
                    value += (highPart << (8 * i));
                    return value;
                }
                b = Bytes[Offset];
                Offset++;
                value |= ((ulong)b << (8 * i));
                mask >>= 1;
            }
            return value;
        }

        public uint ReadUInt32()
        {
            var val = BitConverter.ToUInt32(Bytes, Offset);
            Offset += 4;
            return val;
        }
    }
}
