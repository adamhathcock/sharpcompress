namespace SharpCompress.Compressor.Rar.decode
{

    internal class Decode
    {
        internal Decode()
            : this(new int[2])
        {
        }

        protected Decode(int[] customDecodeNum)
        {
            DecodeLen = new int[16];
            DecodePos = new int[16];
            DecodeNum = customDecodeNum;
        }

        /// <summary> returns the decode Length array</summary>
        /// <returns> decodeLength
        /// </returns>
        internal int[] DecodeLen
        {
            get;
            private set;

        }
        /// <summary> returns the decode num array</summary>
        /// <returns> decodeNum
        /// </returns>
        internal int[] DecodeNum
        {
            get;
            private set;
        }
        /// <summary> returns the decodePos array</summary>
        /// <returns> decodePos
        /// </returns>
        internal int[] DecodePos
        {
            get;
            private set;
        }

        internal int MaxNum
        {
            get;
            set;
        }
    }
}