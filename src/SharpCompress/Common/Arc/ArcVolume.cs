using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Readers;

namespace SharpCompress.Common.Arc
{
    public class ArcVolume : Volume
    {
        public ArcVolume(Stream stream, ReaderOptions readerOptions, int index = 0)
            : base(stream, readerOptions, index) { }
    }
}
