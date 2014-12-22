using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace SharpCompress.Common
{
    public class ReaderExtractionEventArgs<T>:EventArgs
    {
        internal ReaderExtractionEventArgs(T entry)
        {
            Item = entry;
        } 
        public T Item { get; private set; }
    }
}
