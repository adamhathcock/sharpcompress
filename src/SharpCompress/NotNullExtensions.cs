using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SharpCompress.Helpers;

public static class NotNullExtensions
{
    public static IEnumerable<T> Empty<T>(this IEnumerable<T>? source) =>
        source ?? Enumerable.Empty<T>();

    public static IEnumerable<T> Empty<T>(this T? source)
    {
        if (source is null)
        {
            return Enumerable.Empty<T>();
        }
        return source.AsEnumerable();
    }

#if NETFRAMEWORK || NETSTANDARD
    public static T NotNull<T>(this T? obj, string? message = null)
        where T : class
    {
        if (obj is null)
        {
            throw new ArgumentNullException(message ?? "Value is null");
        }
        return obj;
    }

    public static T NotNull<T>(this T? obj, string? message = null)
        where T : struct
    {
        if (obj is null)
        {
            throw new ArgumentNullException(message ?? "Value is null");
        }
        return obj.Value;
    }
#else

    public static T NotNull<T>(
        [NotNull] this T? obj,
        [CallerArgumentExpression(nameof(obj))] string? paramName = null
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(obj, paramName);
        return obj;
    }

    public static T NotNull<T>(
        [NotNull] this T? obj,
        [CallerArgumentExpression(nameof(obj))] string? paramName = null
    )
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(obj, paramName);
        return obj.Value;
    }
#endif
}
