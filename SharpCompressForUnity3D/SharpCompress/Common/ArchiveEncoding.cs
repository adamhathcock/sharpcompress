namespace SharpCompress.Common
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;

    public static class ArchiveEncoding
    {
        [CompilerGenerated]
        private static Encoding <Default>k__BackingField;
        [CompilerGenerated]
        private static Encoding <Password>k__BackingField;

        static ArchiveEncoding()
        {
            Default = Encoding.UTF8;
            Password = Encoding.UTF8;
        }

        public static Encoding Default
        {
            [CompilerGenerated]
            get
            {
                return <Default>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                <Default>k__BackingField = value;
            }
        }

        public static Encoding Password
        {
            [CompilerGenerated]
            get
            {
                return <Password>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                <Password>k__BackingField = value;
            }
        }
    }
}

