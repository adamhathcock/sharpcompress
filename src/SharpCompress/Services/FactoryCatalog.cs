using System;
using System.Collections.Generic;
using SharpCompress.Factories;

namespace SharpCompress.Services;

// Read through on each operation: legacy registrations made after composition must remain visible.
internal sealed class FactoryCatalog(
    Func<IEnumerable<IFactory>> factories,
    Func<IEnumerable<TarWrapper>> wrappers
)
{
    public IEnumerable<IFactory> Factories => factories();
    public IEnumerable<TarWrapper> Wrappers => wrappers();
}
