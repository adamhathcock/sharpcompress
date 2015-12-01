namespace SharpCompress.Common
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;

    public static class ArchiveEncoding
    {
        [CompilerGenerated]
        private static Encoding _Default_k__BackingField;
        [CompilerGenerated]
        private static Encoding _Password_k__BackingField;

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
                return _Default_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                _Default_k__BackingField = value;
            }
        }

        public static Encoding Password
        {
            [CompilerGenerated]
            get
            {
                return _Password_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                _Password_k__BackingField = value;
            }
        }
    }
}

