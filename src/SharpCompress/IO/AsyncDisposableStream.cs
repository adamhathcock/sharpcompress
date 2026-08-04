using System;
using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.IO;

/// <summary>
/// A <see cref="Stream"/> that is guaranteed to be asynchronously disposable on every target framework.
/// </summary>
/// <remarks>
/// <para>
/// On .NET Framework 4.8 and .NET Standard 2.0, <see cref="Stream"/> has no <c>DisposeAsync</c>.
/// <c>Microsoft.Bcl.AsyncInterfaces</c> supplies the <see cref="IAsyncDisposable"/> interface on those
/// targets but cannot retrofit it onto the BCL's <see cref="Stream"/>, and C# will not accept an
/// extension method for the pattern - <c>await using</c> requires a reachable <em>instance</em>
/// <c>DisposeAsync</c>. Deriving from this class instead of <see cref="Stream"/> therefore makes a type
/// usable with <c>await using</c> uniformly, with no conditional compilation at the call site.
/// </para>
/// <para>
/// The fallback below is the same behaviour as the BCL's own default <see cref="Stream.DisposeAsync"/>,
/// so a derived type may call <c>await base.DisposeAsync()</c> unconditionally on any target.
/// </para>
/// </remarks>
public abstract class AsyncDisposableStream : Stream
#if NO_STREAM_DISPOSEASYNC
        , IAsyncDisposable
#endif
{
#if NO_STREAM_DISPOSEASYNC
    public virtual ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
#endif
}
