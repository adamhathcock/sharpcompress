//
// DARTTest.cs
//
// Author:
//       Natalia Portillo <claunia@claunia.com>
//
// Copyright (c) 2016 Natalia Portillo
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
using SharpCompress.Compressors;
using SharpCompress.Compressors.DART;
using System.IO;
using Xunit;

namespace SharpCompress.Test
{
    public class DARTTest : TestBase
    {
        [Fact]
        public void TestRLEStream()
        {
            using (FileStream decFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "dart_rle_decompressed.bin")))
            {
                byte[] decompressed = new byte[decFs.Length];
                decFs.Read(decompressed, 0, decompressed.Length);

                using (FileStream cmpFs = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "dart_rle_compressed.bin")))
                {
                    using (RLEStream decStream = new RLEStream(cmpFs, CompressionMode.Decompress))
                    {
                        byte[] test = new byte[419200];

                        decStream.Read(test, 0, test.Length);

                        Assert.Equal(decompressed, test);
                    }
                }
            }
        }
    }
}
