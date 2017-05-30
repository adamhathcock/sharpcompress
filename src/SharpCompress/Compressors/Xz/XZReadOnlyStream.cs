using System.IO;

namespace SharpCompress.Compressors.Xz
{
    public abstract class XZReadOnlyStream : ReadOnlyStream
    {
        public long StreamStartPosition { get; protected set; }

        public XZReadOnlyStream(Stream stream)
        {
            BaseStream = stream;
            if (!BaseStream.CanRead)
                throw new InvalidDataException("Must be able to read from stream");
            StreamStartPosition = BaseStream.Position;
        }
    }
}
