using System;

public partial class ArcLzwStream
{
    public class BitReader
    {
        private readonly byte[] data;
        private int bitPosition;
        private int bytePosition;

        public BitReader(byte[] inputData)
        {
            data = inputData;
            bitPosition = 0;
            bytePosition = 0;
        }

        public int? ReadBits(int bitCount)
        {
            if (bitCount <= 0 || bitCount > 16)
                throw new ArgumentOutOfRangeException(
                    nameof(bitCount),
                    "Bit count must be between 1 and 16"
                );

            if (bytePosition >= data.Length)
                return null;

            int result = 0;
            int bitsRead = 0;

            while (bitsRead < bitCount)
            {
                if (bytePosition >= data.Length)
                    return null;

                int bitsAvailable = 8 - bitPosition;
                int bitsToRead = Math.Min(bitCount - bitsRead, bitsAvailable);

                int mask = (1 << bitsToRead) - 1;
                result |= ((data[bytePosition] >> bitPosition) & mask) << bitsRead;

                bitPosition += bitsToRead;
                bitsRead += bitsToRead;

                if (bitPosition >= 8)
                {
                    bitPosition = 0;
                    bytePosition++;
                }
            }

            return (ushort)result;
        }
    }
}
