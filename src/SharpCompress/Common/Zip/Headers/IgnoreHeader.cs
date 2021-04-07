using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers
{
    internal class IgnoreHeader : ZipHeader
    {
        public IgnoreHeader(ZipHeaderType type)
            : base(type)
        {
        }

        internal override ValueTask Read(Stream stream, CancellationToken cancellationToken)
        {
            return new();
        }
    }
}