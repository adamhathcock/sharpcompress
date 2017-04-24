using System;

namespace SharpCompress.Common
{
    public class ReaderExtractionEventArgs<T> : EventArgs
    {
        internal ReaderExtractionEventArgs(T entry, params object[] paramList)
        {
            Item = entry;
            ParamList = paramList;
        }

        public T Item { get; private set; }
        public object[] ParamList { get; private set; }
    }
}