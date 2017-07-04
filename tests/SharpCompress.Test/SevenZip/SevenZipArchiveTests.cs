﻿using System;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.SevenZip
{
    public class SevenZipArchiveTests : ArchiveTests
    {
        [Fact]
        public void SevenZipArchive_LZMA_StreamRead()
        {
            ArchiveStreamRead("7Zip.LZMA.7z");
        }

        [Fact]
        public void SevenZipArchive_LZMA_PathRead()
        {
            ArchiveFileRead("7Zip.LZMA.7z");
        }

        [Fact]
        public void SevenZipArchive_PPMd_StreamRead()
        {
            ArchiveStreamRead("7Zip.PPMd.7z");
        }

        [Fact]
        public void SevenZipArchive_PPMd_StreamRead_Extract_All()
        {
            ArchiveStreamReadExtractAll("7Zip.PPMd.7z", CompressionType.PPMd);
        }

        [Fact]
        public void SevenZipArchive_PPMd_PathRead()
        {
            ArchiveFileRead("7Zip.PPMd.7z");
        }
        [Fact]
        public void SevenZipArchive_LZMA2_StreamRead()
        {
            ArchiveStreamRead("7Zip.LZMA2.7z");
        }

        [Fact]
        public void SevenZipArchive_LZMA2_PathRead()
        {
            ArchiveFileRead("7Zip.LZMA2.7z");
        }
        [Fact]
        public void SevenZipArchive_BZip2_StreamRead()
        {
            ArchiveStreamRead("7Zip.BZip2.7z");
        }

        [Fact]
        public void SevenZipArchive_BZip2_PathRead()
        {
            ArchiveFileRead("7Zip.BZip2.7z");
        }

        [Fact]
        public void SevenZipArchive_LZMA_Time_Attributes_PathRead()
        {
            ArchiveFileReadEx("7Zip.LZMA.7z");
        }

        [Fact]
        public void SevenZipArchive_BZip2_Split()
        {
            Assert.Throws<IndexOutOfRangeException>(() => ArchiveStreamRead("Original.7z.001", "Original.7z.002",
                "Original.7z.003", "Original.7z.004", "Original.7z.005",
                "Original.7z.006", "Original.7z.007"));
        }
    }
}
