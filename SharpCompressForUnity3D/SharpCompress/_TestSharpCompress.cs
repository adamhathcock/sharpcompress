namespace SharpCompress
{
    using SharpCompress.Archive.Zip;
    using SharpCompress.Common;
    using SharpCompress.IO;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    internal static class _TestSharpCompress
    {
        public static void Main(string[] args)
        {
            FileStream stream2;
            byte[] buffer;
            byte[] buffer2;
            string path = "ziptest";
            string str2 = "ziptest.zip";
            using (ZipArchive archive = ZipArchive.Create())
            {
                DirectoryInfo info = new DirectoryInfo(path);
                foreach (FileInfo info2 in info.GetFiles())
                {
                    DateTime? modified = null;
                    archive.AddEntry(info2.Name, info2.OpenRead(), true, 0L, modified);
                }
                FileStream stream = new FileStream(str2, FileMode.OpenOrCreate, FileAccess.Write);
                archive.SaveTo(stream, 4);
                stream.Close();
                using (stream2 = new FileStream("ziphead.zip", FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    buffer = new MyHead().Create();
                    stream2.Write(buffer, 0, buffer.Length);
                    OffsetStream stream3 = new OffsetStream(stream2, stream2.Position);
                    archive.SaveTo(stream3, 4);
                }
            }
            using (stream2 = new FileStream("mypack.data.zip", FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                buffer = new MyHead().Create();
                stream2.Write(buffer, 0, buffer.Length);
                using (FileStream stream4 = new FileStream(str2, FileMode.Open, FileAccess.Read))
                {
                    buffer2 = new byte[0x400];
                    int count = 0;
                    while ((count = stream4.Read(buffer2, 0, buffer2.Length)) > 0)
                    {
                        stream2.Write(buffer2, 0, count);
                    }
                }
            }
            using (stream2 = new FileStream("mypack.data.zip", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                buffer2 = new byte[0x400];
                int num2 = stream2.Read(buffer2, 0, buffer2.Length);
                Debug.Assert(num2 == 0x400);
                OffsetStream stream5 = new OffsetStream(stream2, (long) num2);
                ZipArchive archive2 = ZipArchive.Open(stream5, Options.KeepStreamsOpen, null);
                foreach (ZipArchiveEntry entry in archive2.Entries)
                {
                    Console.WriteLine(entry.Key);
                }
                int num3 = 0;
                num3++;
            }
        }

        public class MyHead
        {
            public string Flag = "MyHead";
            public int Id = 0x2766;
            public string Ver = "1.0.1";

            public byte[] Create()
            {
                using (MemoryStream stream = new MemoryStream(0x400))
                {
                    stream.SetLength(0x400L);
                    using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                    {
                        writer.Write(this.Flag);
                        writer.Write(this.Id);
                        writer.Write(this.Ver);
                    }
                    return stream.ToArray();
                }
            }
        }
    }
}

