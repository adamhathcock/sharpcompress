using System.IO;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Common.Rar
{
    /// <summary>
    /// This represents a single file part that exists in a rar volume.  A compressed file is one or many file parts that are spread across one or may rar parts.
    /// </summary>
    internal abstract class RarFilePart : FilePart
    {
        internal RarFilePart(SignatureType mh, FileHeader fh)
        {
            SignatureType = mh;
            FileHeader = fh;
        }
        
        internal SignatureType SignatureType { get; private set; }

        internal FileHeader FileHeader { get; private set; }

        internal override Stream GetRawStream()
        {
            return null;
        }
    }
}