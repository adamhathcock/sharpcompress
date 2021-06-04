using System;

namespace SharpCompress.Common.Dmg
{
    internal static class PartitionFormat
    {
        public static readonly Guid AppleHFS         = new Guid("48465300-0000-11AA-AA11-00306543ECAC");
        public static readonly Guid AppleUFS         = new Guid("55465300-0000-11AA-AA11-00306543ECAC");
        public static readonly Guid AppleBoot        = new Guid("426F6F74-0000-11AA-AA11-00306543ECAC");
        public static readonly Guid AppleRaid        = new Guid("52414944-0000-11AA-AA11-00306543ECAC");
        public static readonly Guid AppleRaidOffline = new Guid("52414944-5F4F-11AA-AA11-00306543ECAC");
        public static readonly Guid AppleLabel       = new Guid("4C616265-6C00-11AA-AA11-00306543ECAC");
    }
}
