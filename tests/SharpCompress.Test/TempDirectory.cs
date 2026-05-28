using System;
using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Test;

internal sealed class TempDirectory : IAsyncDisposable
{
    private const int MaxDeleteAttempts = 5;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(100);

    public TempDirectory(string prefix)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetDirectory(string name)
    {
        var path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public string CreateDirectory(string name)
    {
        var path = System.IO.Path.Combine(Path, name, System.IO.Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    public void ResetDirectory(string name)
    {
        DeleteDirectory(System.IO.Path.Combine(Path, name));
        Directory.CreateDirectory(System.IO.Path.Combine(Path, name));
    }

    public async ValueTask DisposeAsync()
    {
        for (var attempt = 1; attempt <= MaxDeleteAttempts; attempt++)
        {
            try
            {
                DeleteDirectory(Path);
                if (Directory.Exists(Path))
                {
                    throw new IOException(
                        $"Temp test directory '{Path}' still exists after deletion."
                    );
                }

                return;
            }
            catch (Exception ex)
                when (IsRetryableDeleteException(ex) && attempt < MaxDeleteAttempts)
            {
                await Task.Delay(DeleteRetryDelay).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRetryableDeleteException(ex))
            {
                throw new InvalidOperationException(
                    $"Failed to clean up temp test directory '{Path}'.",
                    ex
                );
            }
        }

        throw new InvalidOperationException($"Temp test directory '{Path}' was not cleaned up.");
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private static bool IsRetryableDeleteException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException;
}
