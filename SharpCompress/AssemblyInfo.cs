using System;
using System.Reflection;
using System.Runtime.CompilerServices;


#if PORTABLE
[assembly: AssemblyTitle("SharpCompress.Portable")]
[assembly: AssemblyProduct("SharpCompress.Portable")]
#else

[assembly: AssemblyTitle("SharpCompress")]
[assembly: AssemblyProduct("SharpCompress")]
#endif

#if UNSIGNED
[assembly: InternalsVisibleTo("SharpCompress.Test")]
[assembly: InternalsVisibleTo("SharpCompress.Test.Portable")]
#endif

[assembly: CLSCompliant(true)]
