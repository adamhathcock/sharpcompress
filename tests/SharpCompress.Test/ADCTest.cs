//
// ADC.cs
//
// Author:
//       Natalia Portillo <claunia@claunia.com>
//
// Copyright (c) 2016 © Claunia.com
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System.IO;
using SharpCompress.Compressors;
using SharpCompress.Compressors.ADC;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Crypto;
using Xunit;

namespace SharpCompress.Test
{
    public class ADCTest : TestBase
    {
        [Fact]
        public void TestBuffer()
        {
            using (FileStream decFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_decompressed.bin")))
            {
                byte[] decompressed = new byte[decFs.Length];
                decFs.Read(decompressed, 0, decompressed.Length);

                using (FileStream cmpFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_compressed.bin")))
                {
                    byte[] compressed = new byte[cmpFs.Length];
                    cmpFs.Read(compressed, 0, compressed.Length);
                    byte[] test;

                    ADCBase.Decompress(compressed, out test);

                    Assert.Equal(decompressed, test);
                }
            }
        }

        [Fact]
        public void TestBaseStream()
        {
            using (FileStream decFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_decompressed.bin")))
            {
                byte[] decompressed = new byte[decFs.Length];
                decFs.Read(decompressed, 0, decompressed.Length);

                using (FileStream cmpFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_compressed.bin")))
                {
                    byte[] test;

                    ADCBase.Decompress(cmpFs, out test);

                    Assert.Equal(decompressed, test);
                }
            }
        }

        [Fact]
        public void TestADCStreamWholeChunk()
        {
            using (FileStream decFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_decompressed.bin")))
            {
                byte[] decompressed = new byte[decFs.Length];
                decFs.Read(decompressed, 0, decompressed.Length);

                using (FileStream cmpFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_compressed.bin")))
                {
                    using (ADCStream decStream = new ADCStream(cmpFs, CompressionMode.Decompress))
                    {
                        byte[] test = new byte[262144];

                        decStream.Read(test, 0, test.Length);

                        Assert.Equal(decompressed, test);
                    }
                }
            }
        }

        [Fact]
        public void TestADCStream()
        {
            using (FileStream decFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_decompressed.bin")))
            {
                byte[] decompressed = new byte[decFs.Length];
                decFs.Read(decompressed, 0, decompressed.Length);

                using (FileStream cmpFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "adc_compressed.bin")))
                {
                    using (ADCStream decStream = new ADCStream(cmpFs, CompressionMode.Decompress))
                    {
                        using (MemoryStream decMs = new MemoryStream())
                        {
                            byte[] test = new byte[512];
                            int count = 0;

                            do
                            {
                                count = decStream.Read(test, 0, test.Length);
                                decMs.Write(test, 0, count);
                            }
                            while (count > 0);

                            Assert.Equal(decompressed, decMs.ToArray());
                        }
                    }
                }
            }
        }

        [Fact]
        public void TestCrc32Stream()
        {
            using (FileStream decFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar")))
            {
                var crc32 = new CRC32().GetCrc32(decFs);
                decFs.Seek(0, SeekOrigin.Begin);

                var memory = new MemoryStream();
                var crcStream = new Crc32Stream(memory, 0xEDB88320, 0xFFFFFFFF);
                decFs.CopyTo(crcStream);

                decFs.Seek(0, SeekOrigin.Begin);

                var crc32a = crcStream.Crc;

                var crc32b = Crc32Stream.Compute(memory.ToArray());

                Assert.Equal(crc32, crc32a);
                Assert.Equal(crc32, crc32b);
            }
        }
    }
}
