using System.IO;

namespace SharpCompress.Common
{
    public abstract class FilePart
    {
        internal abstract string FilePartName
        {
            get;
        }

        internal abstract Stream GetStream();
    }
}
