using System.Reflection;
using System.Runtime.CompilerServices;

#if SILVERLIGHT
[assembly: AssemblyTitle("SharpCompress.Silverlight")]
[assembly: AssemblyProduct("SharpCompress.Silverlight")]
#else
#if PORTABLE
[assembly: AssemblyTitle("SharpCompress.Silverlight")]
[assembly: AssemblyProduct("SharpCompress.Silverlight")]
#else

[assembly: AssemblyTitle("SharpCompress")]
[assembly: AssemblyProduct("SharpCompress")]
[assembly:
    InternalsVisibleTo(
        "SharpCompress.Test"
        )]
#endif
#endif