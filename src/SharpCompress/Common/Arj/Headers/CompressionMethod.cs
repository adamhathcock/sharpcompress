using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Common.Arj.Headers
{
    public enum CompressionMethod
    {
        Stored = 0,
        CompressedMost = 1,
        Compressed = 2,
        CompressedFaster = 3,
        CompressedFastest = 4,
        NoDataNoCrc = 8,
        NoData = 9,
        Unknown,
    }
}
