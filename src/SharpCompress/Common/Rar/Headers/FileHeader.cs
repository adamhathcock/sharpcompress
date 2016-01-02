using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class FileHeader : RarHeader
    {
        private const byte SALT_SIZE = 8;

        private const byte NEWLHD_SIZE = 32;

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
            uint lowUncompressedSize = reader.ReadUInt32();

            HostOS = (HostOS)reader.ReadByte();

            FileCRC = reader.ReadUInt32();

            FileLastModifiedTime = Utility.DosDateToDateTime(reader.ReadInt32());

            RarVersion = reader.ReadByte();
            PackingMethod = reader.ReadByte();

            short nameSize = reader.ReadInt16();

            FileAttributes = reader.ReadInt32();

            uint highCompressedSize = 0;
            uint highUncompressedkSize = 0;
            if (FileFlags.HasFlag(FileFlags.LARGE))
            {
                highCompressedSize = reader.ReadUInt32();
                highUncompressedkSize = reader.ReadUInt32();
            }
            else
            {
                if (lowUncompressedSize == 0xffffffff)
                {
                    lowUncompressedSize = 0xffffffff;
                    highUncompressedkSize = int.MaxValue;
                }
            }
            CompressedSize = UInt32To64(highCompressedSize, AdditionalSize);
            UncompressedSize = UInt32To64(highUncompressedkSize, lowUncompressedSize);

            nameSize = nameSize > 4 * 1024 ? (short)(4 * 1024) : nameSize;

            byte[] fileNameBytes = reader.ReadBytes(nameSize);

            switch (HeaderType)
            {
                case HeaderType.FileHeader:
                    {
                        if (FileFlags.HasFlag(FileFlags.UNICODE))
                        {
                            int length = 0;
                            while (length < fileNameBytes.Length
                                   && fileNameBytes[length] != 0)
                            {
                                length++;
                            }
                            if (length != nameSize)
                            {
                                length++;
                                FileName = FileNameDecoder.Decode(fileNameBytes, length);
                            }
                            else
                            {
                                FileName = DecodeDefault(fileNameBytes);
                            }
                        }
                        else
                        {
                            FileName = DecodeDefault(fileNameBytes);
                        }
                        FileName = ConvertPath(FileName, HostOS);
                    }
                    break;
                case HeaderType.NewSubHeader:
                    {
                        int datasize = HeaderSize - NEWLHD_SIZE - nameSize;
                        if (FileFlags.HasFlag(FileFlags.SALT))
                        {
                            datasize -= SALT_SIZE;
                        }
                        if (datasize > 0)
                        {
                            SubData = reader.ReadBytes(datasize);
                        }

                        if (NewSubHeaderType.SUBHEAD_TYPE_RR.Equals(fileNameBytes))
                        {
                            RecoverySectors = SubData[8] + (SubData[9] << 8)
                                              + (SubData[10] << 16) + (SubData[11] << 24);
                        }
                    }
                    break;
            }

            if (FileFlags.HasFlag(FileFlags.SALT))
            {
                Salt = reader.ReadBytes(SALT_SIZE);
            }
            if (FileFlags.HasFlag(FileFlags.EXTTIME))
            {
                // verify that the end of the header hasn't been reached before reading the Extended Time.
                //  some tools incorrectly omit Extended Time despite specifying FileFlags.EXTTIME, which most parsers tolerate.
                if (ReadBytes + reader.CurrentReadByteCount <= HeaderSize - 2)
                {
                    ushort extendedFlags = reader.ReadUInt16();
                    FileLastModifiedTime = ProcessExtendedTime(extendedFlags, FileLastModifiedTime, reader, 0);
                    FileCreatedTime = ProcessExtendedTime(extendedFlags, null, reader, 1);
                    FileLastAccessedTime = ProcessExtendedTime(extendedFlags, null, reader, 2);
                    FileArchivedTime = ProcessExtendedTime(extendedFlags, null, reader, 3);
                }
            }
        }

        //only the full .net framework will do other code pages than unicode/utf8
        private string DecodeDefault(byte[] bytes)
        {
            return ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length);
        }

        private long UInt32To64(uint x, uint y)
        {
            long l = x;
            l <<= 32;
            return l + y;
        }

        private static DateTime? ProcessExtendedTime(ushort extendedFlags, DateTime? time, MarkingBinaryReader reader,
                                                     int i)
        {
            uint rmode = (uint)extendedFlags >> (3 - i) * 4;
            if ((rmode & 8) == 0)
            {
                return null;
            }
            if (i != 0)
            {
                uint DosTime = reader.ReadUInt32();
                time = Utility.DosDateToDateTime(DosTime);
            }
            if ((rmode & 4) == 0)
            {
                time = time.Value.AddSeconds(1);
            }
            uint nanosecondHundreds = 0;
            int count = (int)rmode & 3;
            for (int j = 0; j < count; j++)
            {
                byte b = reader.ReadByte();
                nanosecondHundreds |= (((uint)b) << ((j + 3 - count) * 8));
            }
            //10^-7 to 10^-3
            return time.Value.AddMilliseconds(nanosecondHundreds * Math.Pow(10, -4));
        }

        private static string ConvertPath(string path, HostOS os)
        {
#if NO_FILE
            return path.Replace('\\', '/');
#else
            switch (os)
            {
                case HostOS.MacOS:
                case HostOS.Unix:
                    {
                        if (Path.DirectorySeparatorChar == '\\')
                        {
                            return path.Replace('/', '\\');
                        }
                    }
                    break;
                default:
                    {
                        if (Path.DirectorySeparatorChar == '/')
                        {
                            return path.Replace('\\', '/');
                        }
                    }
                    break;
            }
            return path;
#endif
        }

        internal long DataStartPosition { get; set; }
        internal HostOS HostOS { get; private set; }

        internal uint FileCRC { get; private set; }

        internal DateTime? FileLastModifiedTime { get; private set; }

        internal DateTime? FileCreatedTime { get; private set; }

        internal DateTime? FileLastAccessedTime { get; private set; }

        internal DateTime? FileArchivedTime { get; private set; }

        internal byte RarVersion { get; private set; }

        internal byte PackingMethod { get; private set; }

        internal int FileAttributes { get; private set; }

        internal FileFlags FileFlags
        {
            get { return (FileFlags)base.Flags; }
        }

        internal long CompressedSize { get; private set; }
        internal long UncompressedSize { get; private set; }

        internal string FileName { get; private set; }

        internal byte[] SubData { get; private set; }

        internal int RecoverySectors { get; private set; }

        internal byte[] Salt { get; private set; }

        public override string ToString()
        {
            return FileName;
        }

        public Stream PackedStream { get; set; }
    }
}