using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Archives.Tar
{
    public static class PaxHeaders
    {
        public const string paxGNUSparseNumBlocks = "GNU.sparse.numblocks";
        public const string paxGNUSparseOffset = "GNU.sparse.offset";
        public const string paxGNUSparseNumBytes = "GNU.sparse.numbytes";
        public const string paxGNUSparseMap = "GNU.sparse.map";
        public const string paxGNUSparseName = "GNU.sparse.name";
        public const string paxGNUSparseMajor = "GNU.sparse.major";
        public const string paxGNUSparseMinor = "GNU.sparse.minor";
        public const string paxGNUSparseSize = "GNU.sparse.size";
        public const string paxGNUSparseRealSize = "GNU.sparse.realsize";
    }
    
    // Keywords for the PAX Extended Header
    public static class PaxKeywords
    {
        public const string paxAtime = "atime";
        public const string paxCharset = "charset";
        public const string paxComment = "comment";
        public const string paxCtime = "ctime";// please note that ctime is not a valid pax header.
        public const string paxGid = "gid";
        public const string paxGname = "gname";
        public const string paxLinkpath = "linkpath";
        public const string paxMtime = "mtime";
        public const string paxPath = "path";
        public const string paxSize = "size";
        public const string paxUid = "uid";
        public const string paxUname = "uname";
        public const string paxXattr = "SCHILY.xattr.";
        public const string paxNone = "";
    }

    public class TarArchive : AbstractWritableArchive<TarArchiveEntry, TarVolume>
    {
#if !NO_FILE

        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="readerOptions"></param>
        public static TarArchive Open(string filePath, ReaderOptions readerOptions = null)
        {
            filePath.CheckNotNullOrEmpty("filePath");
            return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="readerOptions"></param>
        public static TarArchive Open(FileInfo fileInfo, ReaderOptions readerOptions = null)
        {
            fileInfo.CheckNotNull("fileInfo");
            return new TarArchive(fileInfo, readerOptions ?? new ReaderOptions());
        }
#endif

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="readerOptions"></param>
        public static TarArchive Open(Stream stream, ReaderOptions readerOptions = null)
        {
            stream.CheckNotNull("stream");
            return new TarArchive(stream, readerOptions ?? new ReaderOptions());
        }

#if !NO_FILE

        public static bool IsTarFile(string filePath)
        {
            return IsTarFile(new FileInfo(filePath));
        }

        public static bool IsTarFile(FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                return false;
            }

            using (Stream stream = fileInfo.OpenRead())
            {
                return IsTarFile(stream);
            }
        }
#endif

        public static bool IsTarFile(Stream stream)
        {
            try
            {
                TarHeader tar = new TarHeader(new ArchiveEncoding());
                tar.Read(new BinaryReader(stream));
                return tar.Name.Length > 0 && Enum.IsDefined(typeof(EntryType), tar.EntryType);
            }
            catch
            {
            }

            return false;
        }

#if !NO_FILE

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="readerOptions"></param>
        internal TarArchive(FileInfo fileInfo, ReaderOptions readerOptions)
            : base(ArchiveType.Tar, fileInfo, readerOptions)
        {
        }

        protected override IEnumerable<TarVolume> LoadVolumes(FileInfo file)
        {
            return new TarVolume(file.OpenRead(), ReaderOptions).AsEnumerable();
        }
#endif

        /// <summary>
        /// Takes multiple seekable Streams for a multi-part archive
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="readerOptions"></param>
        internal TarArchive(Stream stream, ReaderOptions readerOptions)
            : base(ArchiveType.Tar, stream, readerOptions)
        {
        }

        internal TarArchive()
            : base(ArchiveType.Tar)
        {
        }

        protected override IEnumerable<TarVolume> LoadVolumes(IEnumerable<Stream> streams)
        {
            return new TarVolume(streams.First(), ReaderOptions).AsEnumerable();
        }

        protected override IEnumerable<TarArchiveEntry> LoadEntries(IEnumerable<TarVolume> volumes)
        {
            Stream stream = volumes.Single().Stream;
            TarHeader previousHeader = null;
            byte[] previousBytes = null;
            foreach (TarHeader header in TarHeaderFactory.ReadHeader(StreamingMode.Seekable, stream, ReaderOptions.ArchiveEncoding))
            {
                if (header != null)
                {
                    switch (header.EntryType)
                    {
                        case EntryType.GlobalExtendedHeader:
                        case EntryType.PosixExtendedHeader:
                        case EntryType.LongName:
                        {
                            previousHeader = header;
                            var entry = new TarArchiveEntry(this,
                                                            new TarFilePart(previousHeader, stream),
                                                            CompressionType.None);
                            using (var entryStream = entry.OpenEntryStream())
                            {
                                using (var memoryStream = new MemoryStream())
                                {
                                    entryStream.TransferTo(memoryStream);
                                    memoryStream.Position = 0;
                                    previousBytes = memoryStream.ToArray();
                                }
                            }
                            continue;
                        }
                    }

                    if (previousHeader != null && previousHeader.EntryType == EntryType.LongName)
                    {
                        header.Name = ReaderOptions.ArchiveEncoding.Decode(previousBytes).TrimNulls();

                        previousHeader = null;
                        previousBytes = null;
                    }
                    
                    if (previousHeader != null && previousHeader.EntryType == EntryType.PosixExtendedHeader)
                    {
                        mergePAX(header, parsePAX(previousBytes));
                        

                        previousHeader = null;
                        previousBytes = null;
                    }

                    yield return new TarArchiveEntry(this, new TarFilePart(header, stream), CompressionType.None);
                }
            }
        }
        
        // parsePAX parses PAX headers.
        // If an extended header (type 'x') is invalid, ErrHeader is returned
        private  Dictionary<string, string> parsePAX(byte[] previousBytes)
        {
            byte[] s = previousBytes;

            // For GNU PAX sparse format 0.0 support.
            // This function transforms the sparse format 0.0 headers into format 0.1
            // headers since 0.0 headers were not PAX compliant.
            var sparseMap = new List<string>();

            var extHdrs = new Dictionary<string, string>();
            while (s.Length > 0)
            {
                var t = parsePAXRecord(s);
                string key = ReaderOptions.ArchiveEncoding.Decode(t.Item1);
                string value =  ReaderOptions.ArchiveEncoding.Decode(t.Item2);
                byte[] residual = t.Item3; 
                string x =  ReaderOptions.ArchiveEncoding.Decode(t.Item3);
                Console.WriteLine(x);
                s = residual;

                switch (key) {
                    case PaxHeaders.paxGNUSparseOffset: 
                    case PaxHeaders.paxGNUSparseNumBytes:
                    // Validate sparse header order and value.
                    if ((sparseMap.Count%2 == 0 && key != PaxHeaders.paxGNUSparseOffset) ||
                    (sparseMap.Count%2 == 1 && key != PaxHeaders.paxGNUSparseNumBytes) ||
                        value.Contains(","))
                    {
                        extHdrs.Clear();
                        return extHdrs;
                    }

                        sparseMap.Add(value);
                        break;
                    default:
                    // According to PAX specification, a value is stored only if it is
                    // non-empty. Otherwise, the key is deleted.
                    if (value.Length > 0)
                    {
                        extHdrs[key] = value;
                    } else
                    {
                        extHdrs.Remove(key);
                    }

                        break;
                }
            }
            if (sparseMap.Count> 0)
            {
                extHdrs[PaxHeaders.paxGNUSparseMap] = string.Join(",", sparseMap);
            }

            return extHdrs;
        }
        
        // parsePAXRecord parses the input PAX record string into a key-value pair.
        // If parsing is successful, it will slice off the currently read record and
        // return the remainder as r.
        //
        // A PAX record is of the following form:
        //	"%d %s=%s\n" % (size, key, value)
        private Tuple<byte[], byte[], byte[]> parsePAXRecord(byte[] s)  {
            // The size field ends at the first space.
            var sp = Array.IndexOf(s, (byte)' ');
            if (sp == -1)
            {
                return Tuple.Create(Array.Empty<byte>(), Array.Empty<byte>(), s);
            }

            // Parse the first token as a decimal integer.
            var x = s.Take(sp).ToArray();
            var n = Convert.ToInt64(ReaderOptions.ArchiveEncoding.Decode(x), 10);  // Intentionally parse as native int
            if (n < 5 || s.Length < n) {
                return Tuple.Create(Array.Empty<byte>(), Array.Empty<byte>(), s);
            }

            // Extract everything between the space and the final newline.
            var rec = s.Skip(sp + 1).Take((int)n -sp - 2).ToArray();
            var nl = s.Skip((int)n-1).Take(1).Single();
            var rem = s.Skip((int)n).ToArray();
            if (nl != '\n') {
                return Tuple.Create(Array.Empty<byte>(), Array.Empty<byte>(), s);
            }

            // The first equals separates the key from the value.
            var eq = Array.IndexOf(rec, (byte)'=');
            if (eq == -1) {
                return Tuple.Create(Array.Empty<byte>(), Array.Empty<byte>(), s);
            }
            return Tuple.Create( rec.Take(eq).ToArray(), rec.Skip(eq+1).ToArray(), rem);
        }
        
        // mergePAX merges well known headers according to PAX standard.
        // In general headers with the same name as those found
        // in the header struct overwrite those found in the header
        // struct with higher precision or longer values. Esp. useful
        // for name and linkname fields.
        private void mergePAX(TarHeader hdr, Dictionary<string, string> headers)  
        {
            foreach (var kv in headers) 
            {
                switch (kv.Key) 
                {
                    case PaxKeywords.paxPath:
                        hdr.Name = kv.Value;
                        break;
                    //case PaxKeywords.paxLinkpath:
                    //hdr.Linkname = v
                    //case PaxKeywords.paxUname:
                    //hdr.Uname = v
                    //case PaxKeywords.paxGname:
                    //hdr.Gname = v
                    //case PaxKeywords.paxUid:
                    //id64, err = strconv.ParseInt(v, 10, 64)
                    //hdr.Uid = int(id64) // Integer overflow possible
                    //case PaxKeywords.paxGid:
                    //id64, err = strconv.ParseInt(v, 10, 64)
                    //hdr.Gid = int(id64) // Integer overflow possible
                    //case PaxKeywords.paxAtime:
                    //hdr.AccessTime, err = parsePAXTime(v)
                    case PaxKeywords.paxMtime:
                        hdr.LastModifiedTime = parsePAXTime(kv.Value).DateTime;
                        break;
                    //case PaxKeywords.paxCtime:
                    //hdr.ChangeTime, err = parsePAXTime(v)
                    case PaxKeywords.paxSize:
                        hdr.Size = long.Parse(kv.Value);
                        break;
                    /*default:
                    if (kv.Key.StartsWith(PaxKeywords.paxXattr)) {
                        if hdr.Xattrs == nil {
                            hdr.Xattrs = make(map[string]string)
                        }
                        hdr.Xattrs[k[len(paxXattr):]] = v
                    }*/
                }
            }
        }
        
        // parsePAXTime takes a string of the form %d.%d as described in the PAX
        // specification. Note that this implementation allows for negative timestamps,
        // which is allowed for by the PAX specification, but not always portable.
        private static DateTimeOffset parsePAXTime(string s)
        {
            //const int maxNanoSecondDigits = 9;

            // Split string into seconds and sub-seconds parts.
            var ss = s;
            var sn = "";
            var pos = s.IndexOf('.');
            if (pos  >= 0)
            {
                ss = s.Substring(0, pos);
                sn = s.Substring(pos + 1);
            }

            // Parse the seconds.
            var secs = long.Parse(ss);
           // if (sn.Length == 0)
            //{
                return DateTimeOffset.FromUnixTimeSeconds(secs);
            /*}

            // Parse the nanoseconds.
            if (sn.Trim("0123456789".ToCharArray()) != "") {
                return DateTimeOffset.MinValue;
            }
            while (sn.Length < maxNanoSecondDigits)
            {
                sn += "0"; // Right pad
            }

            if (sn.Length > maxNanoSecondDigits) {
                sn = sn.Substring(0, maxNanoSecondDigits); // Right truncate
        }

            var nsecs = long.Parse(sn); // Must succeed
            if (ss.Length > 0 && ss[0] == '-')
            {
                return DateTimeOffset.FromUnixTimeSeconds(secs); // Negative correction
            }
            return time.Unix(secs, int64(nsecs)), nil*/
        }

        public static TarArchive Create()
        {
            return new TarArchive();
        }

        protected override TarArchiveEntry CreateEntryInternal(string filePath,
                                                               Stream source,
                                                               long size,
                                                               DateTime? modified,
                                                               bool closeStream)
        {
            return new TarWritableArchiveEntry(this,
                                               source,
                                               CompressionType.Unknown,
                                               filePath,
                                               size,
                                               modified,
                                               closeStream);
        }

        protected override void SaveTo(Stream stream,
                                       WriterOptions options,
                                       IEnumerable<TarArchiveEntry> oldEntries,
                                       IEnumerable<TarArchiveEntry> newEntries)
        {
            using (var writer = new TarWriter(stream, new TarWriterOptions(options)))
            {
                foreach (var entry in oldEntries.Concat(newEntries)
                                                .Where(x => !x.IsDirectory))
                {
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        writer.Write(entry.Key, entryStream, entry.LastModifiedTime, entry.Size);
                    }
                }
            }
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            var stream = Volumes.Single().Stream;
            stream.Position = 0;
            return TarReader.Open(stream);
        }
    }
}