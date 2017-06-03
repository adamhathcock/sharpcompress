using SharpCompress.Compressor.Rar.VM;

namespace SharpCompress.Compressor.Rar
{
    internal class UnpackFilter
    {
        internal UnpackFilter()
        {
            Program = new VMPreparedProgram();
        }

        internal int BlockStart
        {
            get;
            set;
        }

        internal int BlockLength
        {
            get;
            set;
        }

        internal int ExecCount
        {
            get;
            set;
        }

        internal bool NextWindow
        {
            get;
            set;
        }

        // position of parent filter in Filters array used as prototype for filter
        // in PrgStack array. Not defined for filters in Filters array.
        internal int ParentFilter
        {
            get;
            set;
        }

        internal VMPreparedProgram Program
        {
            get;
            set;
        }
    }
}