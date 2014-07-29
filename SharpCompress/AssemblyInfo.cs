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
        "SharpCompress.Test, PublicKey=0024000004800000940000000602000000240000525341310004000001000100d3bca873d4a3793947789393295f21e04b010cf245fbb2fd1ded5471508af87d0b7a0eda88efc9315713b43f545aa8c2c2c3cd799ce24607e1ca2ecb478a0ebc8db6e935d65f4074323e292f044b6642f4b80be51abe601bca33da0da5c178f228df2a3ff8e47ee5ca6b52f4012d7cfe7658fb4cbc4fc3067f28e7c9ef0b06b6"
        )]
[assembly:
InternalsVisibleTo(
        "SharpCompress.Test.Portable, PublicKey=0024000004800000940000000602000000240000525341310004000001000100d3bca873d4a3793947789393295f21e04b010cf245fbb2fd1ded5471508af87d0b7a0eda88efc9315713b43f545aa8c2c2c3cd799ce24607e1ca2ecb478a0ebc8db6e935d65f4074323e292f044b6642f4b80be51abe601bca33da0da5c178f228df2a3ff8e47ee5ca6b52f4012d7cfe7658fb4cbc4fc3067f28e7c9ef0b06b6"
        )]
#endif