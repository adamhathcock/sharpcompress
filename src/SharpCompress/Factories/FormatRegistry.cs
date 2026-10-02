using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpCompress.Factories;

/// <summary>
/// An immutable, ordered collection of format factories and compressed TAR wrappers.
/// Factories must support concurrent calls and return independent archive and stream instances.
/// </summary>
public sealed class FormatRegistry
{
    public static FormatRegistry Default { get; } =
        new([
            new ZipFactory(),
            new RarFactory(),
            new TarFactory(),
            new GZipFactory(),
            new LzwFactory(),
            new ArcFactory(),
            new ArjFactory(),
            new AceFactory(),
            new SevenZipFactory(),
        ]);

    public static FormatRegistry Empty { get; } = new([], []);

    public IReadOnlyList<IFactory> Factories { get; }
    public IReadOnlyList<TarWrapper> TarWrappers { get; }
    public int MaximumRewindBufferSize { get; }

    /// <summary>
    /// Copies the supplied collections. Their order defines recognition priority.
    /// </summary>
    public FormatRegistry(
        IEnumerable<IFactory> factories,
        IEnumerable<TarWrapper>? tarWrappers = null
    )
    {
        ThrowHelper.ThrowIfNull(factories);
        var factoryArray = factories.ToArray();
        var wrapperArray = (tarWrappers ?? TarWrapper.Wrappers).ToArray();
        if (
            factoryArray.Any(factory => factory is null)
            || wrapperArray.Any(wrapper => wrapper is null)
        )
        {
            throw new ArgumentException("Registries cannot contain null members.");
        }
        Factories = Array.AsReadOnly(factoryArray);
        TarWrappers = Array.AsReadOnly(wrapperArray);
        MaximumRewindBufferSize =
            wrapperArray.Length == 0
                ? 0
                : wrapperArray.Max(wrapper => wrapper.MinimumRewindBufferSize);
    }

    /// <summary>
    /// Adds or replaces a factory by known archive type, or by name for custom formats.
    /// Replacement retains its position; a new factory is appended unless prepend is true.
    /// </summary>
    public FormatRegistry With(IFactory factory, bool prepend = false)
    {
        ThrowHelper.ThrowIfNull(factory);
        var factories = Factories.ToList();
        var index = factories.FindIndex(existing =>
            factory.KnownArchiveType is { } type
                ? existing.KnownArchiveType == type
                : string.Equals(existing.Name, factory.Name, StringComparison.Ordinal)
        );
        if (index >= 0)
        {
            factories[index] = factory;
        }
        else if (prepend)
        {
            factories.Insert(0, factory);
        }
        else
        {
            factories.Add(factory);
        }
        return new FormatRegistry(factories, TarWrappers);
    }

    /// <summary>Creates a registry with the supplied ordered TAR wrapper collection.</summary>
    public FormatRegistry WithTarWrappers(IEnumerable<TarWrapper> wrappers)
    {
        ThrowHelper.ThrowIfNull(wrappers);
        return new FormatRegistry(Factories, wrappers);
    }
}
