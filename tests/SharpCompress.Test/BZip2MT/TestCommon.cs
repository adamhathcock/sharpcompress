using System;
using System.Diagnostics;
using System.IO;
using SharpCompress.Compressors.BZip2MT.InputStream;
using SharpCompress.Compressors.BZip2MT.OutputStream;
using Xunit;
using Xunit.Abstractions;
namespace SharpCompress.Test.BZip2MT
{
    public static class TestCommon
    {

        /// <summary>
        /// Common test routine for Multi Threaded compression + Single Threaded decompression
        /// </summary>
        /// <param name="console"><see cref="ITestOutputHelper"/></param>
        /// <param name="inputStream">Input stream, must be seekable</param>
        /// <param name="compressMultiThread">If true, will use Parallel Compressor</param>
        /// <param name="decompressMultiThread">If true, will use Parallel Decompressor</param>
        /// <param name="threads">Maximum number of threads to use for compression, if 0 will use Environment.ProcessorCount</param>
        /// <param name="outputBufferSize">Size for temporary output memory buffer</param>
        /// <param name="copyBufferSize">Size for temporary copy buffer</param>
        /// <param name="saveFileOnFail">If true, will save inputStream data to a file</param>
        /// <returns>(Time Compression, Time Decompression)</returns>
        public static (double, double) GenericTest(ITestOutputHelper console, Stream inputStream, bool compressMultiThread, bool decompressMultiThread, int outputBufferSize = 8388608, int copyBufferSize = 8388608, bool saveFileOnFail = false)
        {
            void DebugSaveInputStream()
            {
                string randomFile = Path.GetRandomFileName();
                console.WriteLine($"    Saving input data to {randomFile}");
                using FileStream fs = new FileStream(randomFile, FileMode.Create, FileAccess.Write);
                inputStream.Position = 0;
                inputStream.CopyTo(fs);
                fs.Flush();
                fs.Close();
            }

            double timeCompress = 0;
            double timeDecompress = 0;

            using MemoryStream output = new MemoryStream(outputBufferSize);
            using MemoryStream outputDecompressed = new MemoryStream(outputBufferSize);

            try
            {
                // compress input
                Stopwatch swCompress = new Stopwatch();
                swCompress.Start();
                using Stream compressor = compressMultiThread
                    ? new BZip2ParallelOutputStream(output, false, 9)
                    : new BZip2OutputStream(output, false, 9);
                inputStream.CopyTo(compressor, copyBufferSize);
                compressor.Close();
                swCompress.Stop();
                timeCompress = swCompress.ElapsedMilliseconds;
                console.WriteLine($"    {timeCompress} ms {(compressMultiThread ? "MT": "ST")} compression time... ");

                // reset output position
                output.Position = 0;

                // decompress output
                Stopwatch SwDecompress = new Stopwatch();
                SwDecompress.Start();
                using Stream decompressor = decompressMultiThread
                    ? new BZip2ParallelInputStream(output, false)
                    : new BZip2InputStream(output, false);
                decompressor.CopyTo(outputDecompressed, copyBufferSize);
                SwDecompress.Stop();
                timeDecompress = SwDecompress.ElapsedMilliseconds;
                console.WriteLine($"    {timeDecompress} ms {(decompressMultiThread ? "MT": "ST")} decompression time");
            } catch (Exception ex)
            {
                if (saveFileOnFail)
                {
                    DebugSaveInputStream();
                }

                Assert.Fail($"Exception was thrown... {ex}");
            }

            if (inputStream.Length != outputDecompressed.Length)
            {
                Assert.Fail($"Decompressed stream length mismatch, expecting {inputStream.Length}, got {outputDecompressed.Length}");
            }

            inputStream.Position = 0;
            outputDecompressed.Position = 0;

            for (int i = 0; i < inputStream.Length; i++)
            {
                int expect = inputStream.ReadByte();
                int value = outputDecompressed.ReadByte();
                if ( expect != value)
                {
                    if (saveFileOnFail)
                    {
                        DebugSaveInputStream();
                        Assert.Fail($"bytes differ at position {i}, expected {expect}, got {value}");
                    }
                }
            }

            return (timeCompress, timeDecompress);
        }

        public enum RandomDataMode
        {
            SingleByteValue,
            RandomBytes,
            RandomBytesRepeat,
        }

        public enum TestMode
        {
            CMT_DST, // multi thread compress, single thread decompress
            CST_DST, // single thread compress, single thread decompress
            CMT_DMT, // multi thread compress, multi thread decompress
            CST_DMT, // single thread compress, multi thread decompress
        }

        public static void RandomLongTest_X(ITestOutputHelper console, int repeat, RandomDataMode dataMode, TestMode testMode, int len = 9000000)
        {
            Random random = new Random();

            int copyBufferSize = 8388608;
            int outBufferSize = 8388608;
            int repeatStreaks = 64;

            double totalCompressionTimeMs = 0;
            double totalDecompressionTimeMs = 0;

            for (int r = 0; r < repeat; r++)
            {
                byte[] bigBuffer = new byte[len];
                switch (dataMode)
                {
                    case RandomDataMode.RandomBytes:
                    {
                        random.NextBytes(bigBuffer);
                        break;
                    }
                    case RandomDataMode.RandomBytesRepeat:
                    {
                        random.NextBytes(bigBuffer);

                        int offset = 0;
                        for (int rs = 0; rs < repeatStreaks; rs++)
                        {
                            int newoffset = random.Next(0, (len - 10000) / repeatStreaks);
                            offset += newoffset;
                            int count = random.Next(0, 512);
                            byte val = bigBuffer[offset++];
                            for (int i = 0; i < count; i++)
                            {
                                bigBuffer[offset++] = val;
                            }
                        }
                        break;
                    }
                    case RandomDataMode.SingleByteValue:
                    {
                        byte value = (byte)(random.Next() & 0xFF);
                        for (int i = 0; i < len; i++)
                        {
                            bigBuffer[i] = value;
                        }
                        break;
                    }
                }

                MemoryStream ms = new MemoryStream(bigBuffer);
                double timeC = 0, timeD = 0;
                switch (testMode)
                {
                    default: throw new ArgumentException("Unknown test mode");
                    case TestMode.CMT_DMT:
                    {
                        (timeC, timeD) = GenericTest(console, ms, true, true, outBufferSize, copyBufferSize, true);
                        break;
                    }
                    case TestMode.CST_DMT:
                    {
                        (timeC, timeD) = GenericTest(console, ms, false, true, outBufferSize, copyBufferSize, true);
                        break;
                    }
                    case TestMode.CST_DST:
                    {
                        (timeC, timeD) = GenericTest(console, ms, false, false, outBufferSize, copyBufferSize, true);
                        break;
                    }
                    case TestMode.CMT_DST:
                    {
                        (timeC, timeD) = GenericTest(console, ms, true, false, outBufferSize, copyBufferSize, true);
                        break;
                    }
                }

                totalCompressionTimeMs += timeC;
                totalDecompressionTimeMs += timeD;
            }
            console.WriteLine($"DONE {testMode}");
            console.WriteLine($"AVERAGE {totalCompressionTimeMs / repeat} ms compression time... ");
            console.WriteLine($"AVERAGE {totalDecompressionTimeMs / repeat} ms decompression time... ");
        }
    }
}
