using System;
using System.Threading.Tasks;

namespace SharpCompress.IO;

/// <summary>
/// Makes any resource usable with <c>await using</c>, disposing it asynchronously when the runtime type
/// supports it and synchronously otherwise.
/// </summary>
/// <remarks>
/// Needed for locals whose <em>static</em> type is <see cref="System.IO.Stream"/> (or another type that
/// only sometimes has <c>DisposeAsync</c>), where <c>await using</c> cannot bind directly on
/// .NET Framework 4.8 / .NET Standard 2.0. Unlike a compile-time guard, this picks the asynchronous path
/// based on the runtime type, so a stream that really is asynchronously disposable is disposed that way on
/// every target framework. Prefer deriving from <see cref="AsyncDisposableStream"/> where the type is ours.
/// </remarks>
internal readonly struct AsyncDisposeScope(IDisposable? resource) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        if (resource is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        resource?.Dispose();
        return default;
    }

    /// <summary>
    /// Mirrors <c>ConfiguredAsyncDisposable</c> so <c>await using</c> can specify context capture without
    /// boxing this struct through <see cref="IAsyncDisposable"/>.
    /// </summary>
    public ConfiguredAsyncDisposeScope ConfigureAwait(bool continueOnCapturedContext) =>
        new(resource, continueOnCapturedContext);
}