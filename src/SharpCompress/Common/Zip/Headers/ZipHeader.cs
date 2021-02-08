using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers
{
    internal abstract class ZipHeader
    {
        protected ZipHeader(ZipHeaderType type)
        {
            ZipHeaderType = type;
            HasData = true;
        }

        internal ZipHeaderType ZipHeaderType { get; }

        internal abstract ValueTask Read(Stream stream, CancellationToken cancellationToken);

        internal bool HasData { get; set; }
    }
}