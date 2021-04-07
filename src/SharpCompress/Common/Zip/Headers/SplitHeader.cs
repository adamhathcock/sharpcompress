using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers
{
    internal class SplitHeader : ZipHeader
    {
        public SplitHeader()
            : base(ZipHeaderType.Split)
        {
        }

        internal override ValueTask Read(Stream stream, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}