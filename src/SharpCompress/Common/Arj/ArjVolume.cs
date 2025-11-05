using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Readers;

namespace SharpCompress.Common.Arj
{
    public class ArjVolume : Volume
    {
        public ArjVolume(Stream stream, ReaderOptions readerOptions, int index = 0)
            : base(stream, readerOptions, index) { }

        public override bool IsFirstVolume
        {
            get { return true; }
        }

        /// <summary>
        /// ArjArchive is part of a multi-part archive.
        /// </summary>
        public override bool IsMultiVolume
        {
            get { return false; }
        }

        internal IEnumerable<ArjFilePart> GetVolumeFileParts()
        {
            return new List<ArjFilePart>();
        }
    }
}
