using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using ZstdSharp;

namespace SharpCompress.Writers.ZStandard
{
    public static class ZStandard
    {
        public static readonly byte[] Magic = { 0xFD, 0x2F, 0xB5, 0x28};
    }

    [Flags]
    public enum FrameHeaderDescriptor
    {
        Dictionary_ID_Flag = 0,
        Dictionary_ID_Flag2 = 1,
        Content_Checksum_Flag = 2,
        Single_Segment_Flag = 5,
        Frame_Content_Size_Flag = 6,
        Frame_Content_Size_Flag2 = 7
        
    }

    public class FrameHeader
    {
        //private byte Descriptor = 0x0;
    }
    public sealed class ZStandardWriter : AbstractWriter
    {

        public ZStandardWriter(Stream destination, ZStandardWriterOptions? options = null)
            : base(ArchiveType.ZStandard, options ?? new ZStandardWriterOptions())
        {
            if (WriterOptions.LeaveStreamOpen)
            {
                destination = new NonDisposingStream(destination);
            }
            InitalizeStream(new CompressionStream(destination));
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                OutputStream.Dispose();
            }
            base.Dispose(isDisposing);
        }

        public override void Write(string filename, Stream source, DateTime? modificationTime)
        {
            GZipStream stream = (GZipStream)OutputStream;
            stream.FileName = filename;
            stream.LastModified = modificationTime;
            source.TransferTo(stream);
        }
    }
}