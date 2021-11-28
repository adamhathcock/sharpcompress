using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZstdSharp.Unsafe;

namespace ZstdSharp
{
    public static unsafe class DictBuilder
    {
        public static byte[] TrainFromBuffer(IEnumerable<byte[]> samples, int dictCapacity = DefaultDictCapacity)
        {
            var ms = new MemoryStream();
            var samplesSizes = samples.Select(sample =>
            {
                ms.Write(sample, 0, sample.Length);
                return (nuint) sample.Length;
            }).ToArray();

            var dictBuffer = new byte[dictCapacity];
            fixed (byte* dictBufferPtr = dictBuffer)
            fixed (byte* samplesBufferPtr = ms.GetBuffer())
            fixed (nuint* samplesSizesPtr = samplesSizes)
            {
                var dictSize = (int) Methods
                    .ZDICT_trainFromBuffer(dictBufferPtr, (nuint) dictCapacity, samplesBufferPtr, samplesSizesPtr,
                        (uint) samplesSizes.Length)
                    .EnsureZdictSuccess();

                if (dictCapacity != dictSize)
                    Array.Resize(ref dictBuffer, dictSize);

                return dictBuffer;
            }
        }

        public const int DefaultDictCapacity = 112640; // Used by zstd utility by default
    }
}
