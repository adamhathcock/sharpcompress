using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip
{
    public class Zip64Tests : WriterTests
    {
        public Zip64Tests()
            : base(ArchiveType.Zip)
        {
        }

        // 4GiB + 1
        private const long FOUR_GB_LIMIT = ((long)uint.MaxValue) + 1;

        [Trait("format", "zip64")]
        public void Zip64_Single_Large_File()
        {
            // One single file, requires zip64
            RunSingleTest(1, FOUR_GB_LIMIT, set_zip64: true, forward_only: false);
        }

        [Trait("format", "zip64")]
        public void Zip64_Two_Large_Files()
        {
            // One single file, requires zip64
            RunSingleTest(2, FOUR_GB_LIMIT, set_zip64: true, forward_only: false);
        }

        [Trait("format", "zip64")]
        public void Zip64_Two_Small_files()
        {
            // Multiple files, does not require zip64
            RunSingleTest(2, FOUR_GB_LIMIT / 2, set_zip64: false, forward_only: false);
        }

        [Trait("format", "zip64")]
        public void Zip64_Two_Small_files_stream()
        {
            // Multiple files, does not require zip64, and works with streams
            RunSingleTest(2, FOUR_GB_LIMIT / 2, set_zip64: false, forward_only: true);
        }

        [Trait("format", "zip64")]
        public void Zip64_Two_Small_Files_Zip64()
        {
            // Multiple files, use zip64 even though it is not required
            RunSingleTest(2, FOUR_GB_LIMIT / 2, set_zip64: true, forward_only: false);
        }

        [Trait("format", "zip64")]
        public void Zip64_Single_Large_File_Fail()
        {
            try
            {
                // One single file, should fail
                RunSingleTest(1, FOUR_GB_LIMIT, set_zip64: false, forward_only: false);
                throw new Exception("Test did not fail?");
            }
            catch (NotSupportedException)
            {
            }
        }

        [Trait("zip64", "true")]
        public void Zip64_Single_Large_File_Zip64_Streaming_Fail()
        {
            try
            {
                // One single file, should fail (fast) with zip64
                RunSingleTest(1, FOUR_GB_LIMIT, set_zip64: true, forward_only: true);
                throw new Exception("Test did not fail?");
            }
            catch (NotSupportedException)
            {
            }
        }

        [Trait("zip64", "true")]
        public void Zip64_Single_Large_File_Streaming_Fail()
        {
            try
            {
                // One single file, should fail once the write discovers the problem
                RunSingleTest(1, FOUR_GB_LIMIT, set_zip64: false, forward_only: true);
                throw new Exception("Test did not fail?");
            }
            catch (NotSupportedException)
            {
            }
        }

        public void RunSingleTest(long files, long filesize, bool set_zip64, bool forward_only, long write_chunk_size = 1024 * 1024, string filename = "zip64-test.zip")
        {
            filename = Path.Combine(SCRATCH2_FILES_PATH, filename);

            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            if (!File.Exists(filename))
            {
                CreateZipArchive(filename, files, filesize, write_chunk_size, set_zip64, forward_only);
            }

            var resForward = ReadForwardOnly(filename);
            if (resForward.Item1 != files)
            {
                throw new Exception($"Incorrect number of items reported: {resForward.Item1}, should have been {files}");
            }

            if (resForward.Item2 != files * filesize)
            {
                throw new Exception($"Incorrect combined size reported: {resForward.Item2}, should have been {files * filesize}");
            }

            var resArchive = ReadArchive(filename);
            if (resArchive.Item1 != files)
            {
                throw new Exception($"Incorrect number of items reported: {resArchive.Item1}, should have been {files}");
            }

            if (resArchive.Item2 != files * filesize)
            {
                throw new Exception($"Incorrect number of items reported: {resArchive.Item2}, should have been {files * filesize}");
            }
        }

        public void CreateZipArchive(string filename, long files, long filesize, long chunksize, bool set_zip64, bool forward_only)
        {
            var data = new byte[chunksize];

            // Use deflate for speed
            var opts = new ZipWriterOptions(CompressionType.Deflate) { UseZip64 = set_zip64 };

            // Use no compression to ensure we hit the limits (actually inflates a bit, but seems better than using method==Store)
            var eo = new ZipWriterEntryOptions() { DeflateCompressionLevel = Compressors.Deflate.CompressionLevel.None };

            using (var zip = File.OpenWrite(filename))
            using (var st = forward_only ? (Stream)new ForwardOnlyStream(zip) : zip)
            using (var zipWriter = (ZipWriter)WriterFactory.Open(st, ArchiveType.Zip, opts))
            {

                for (var i = 0; i < files; i++)
                {
                    using (var str = zipWriter.WriteToStream(i.ToString(), eo))
                    {
                        var left = filesize;
                        while (left > 0)
                        {
                            var b = (int)Math.Min(left, data.Length);
                            str.Write(data, 0, b);
                            left -= b;
                        }
                    }
                }
            }
        }

        public Tuple<long, long> ReadForwardOnly(string filename)
        {
            long count = 0;
            long size = 0;
            Common.Zip.ZipEntry prev = null;
            using (var fs = File.OpenRead(filename))
            using (var rd = ZipReader.Open(fs, new ReaderOptions() { LookForHeader = false }))
            {
                while (rd.MoveToNextEntry())
                {
                    using (rd.OpenEntryStream())
                    { }

                    count++;
                    if (prev != null)
                    {
                        size += prev.Size;
                    }

                    prev = rd.Entry;
                }
            }

            if (prev != null)
            {
                size += prev.Size;
            }

            return new Tuple<long, long>(count, size);
        }

        public Tuple<long, long> ReadArchive(string filename)
        {
            using (var archive = ArchiveFactory.Open(filename))
            {
                return new Tuple<long, long>(
                    archive.Entries.Count(),
                    archive.Entries.Select(x => x.Size).Sum()
                );
            }
        }
    }
}