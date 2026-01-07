using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common.Arj;
using SharpCompress.Readers;

namespace SharpCompress.Common.Ace
{
    public class AceVolume : Volume
    {
        public AceVolume(Stream stream, ReaderOptions readerOptions, int index = 0)
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

        internal IEnumerable<AceFilePart> GetVolumeFileParts()
        {
            return new List<AceFilePart>();
        }
    }
}
