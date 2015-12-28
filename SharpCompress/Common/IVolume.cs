using System;
#if !DOTNET51
using System.IO;
#endif

namespace SharpCompress.Common
{
    public interface IVolume : IDisposable
    {
    }
}