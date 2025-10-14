using System;

namespace SharpCompress.Test;

public static class OperatingSystemExtensions
{
    public static bool IsWindows(this OperatingSystem os) =>
        os.Platform == PlatformID.Win32NT
        || os.Platform == PlatformID.Win32Windows
        || os.Platform == PlatformID.Win32S;
}
