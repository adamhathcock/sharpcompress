using System;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("SharpCompress")]
[assembly: AssemblyProduct("SharpCompress")]
[assembly: InternalsVisibleTo("SharpCompress.Test" + SharpCompress.AssemblyInfo.PublicKeySuffix)]
[assembly: CLSCompliant(true)]

namespace SharpCompress
{
    /// <summary>
    /// Just a static class to house the public key, to avoid repetition.
    /// </summary>
    internal static class AssemblyInfo
    {
        internal const string PublicKeySuffix =
            ",PublicKey=002400000480000094000000060200000024000052534131000400000100010059acfa17d26c44" +
            "7a4d03f16eaa72c9187c04f16e6569dd168b080e39a6f5c9fd00f28c768cd8e9a089d5a0e1b34c" +
            "cd971488e7afe030ce5ce8df2053cf12ec89f6d38065c434c09ee6af3ee284c5dc08f44774b679" +
            "bf39298e57efe30d4b00aecf9e4f6f8448b2cb0146d8956dfcab606cc64a0ac38c60a7d78b0d65" +
            "d3b98dc0";
    }
}
