using System;
using System.Reflection;
using System.Runtime.CompilerServices;

#if PORTABLE
[assembly: AssemblyTitle("SharpCompress.Portable")]
[assembly: AssemblyProduct("SharpCompress.Portable")]
#else

[assembly: AssemblyTitle("SharpCompress")]
[assembly: AssemblyProduct("SharpCompress")]
[assembly:
    InternalsVisibleTo(
        "SharpCompress.Test, PublicKey=002400000480000094000000060200000024000052534131000400000100010005d6ae1b0f6875393da83c920a5b9408f5191aaf4e8b3c2c476ad2a11f5041ecae84ce9298bc4c203637e2fd3a80ad5378a9fa8da1363e98cea45c73969198a4b64510927c910001491cebbadf597b22448ad103b0a4007e339faf8fe8665dcdb70d65b27ac05b1977c0655fad06b372b820ecbdccf10a0f214fee0986dfeded"
        )]
[assembly:
InternalsVisibleTo(
        "SharpCompress.Test.Portable, PublicKey=002400000480000094000000060200000024000052534131000400000100010005d6ae1b0f6875393da83c920a5b9408f5191aaf4e8b3c2c476ad2a11f5041ecae84ce9298bc4c203637e2fd3a80ad5378a9fa8da1363e98cea45c73969198a4b64510927c910001491cebbadf597b22448ad103b0a4007e339faf8fe8665dcdb70d65b27ac05b1977c0655fad06b372b820ecbdccf10a0f214fee0986dfeded"
        )]
#endif

[assembly: CLSCompliant(true)]
