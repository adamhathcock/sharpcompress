using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Compressors.Xz.Filters;

public abstract class BlockFilter : ReadOnlyStream
{
    [CLSCompliant(false)]
    public enum FilterTypes : ulong
    {
        DELTA = 0x03,
        ARCH_x86_FILTER = 0x04,
        ARCH_PowerPC_FILTER = 0x05,
        ARCH_IA64_FILTER = 0x06,
        ARCH_ARM_FILTER = 0x07,
        ARCH_ARMTHUMB_FILTER = 0x08,
        ARCH_SPARC_FILTER = 0x09,
        LZMA2 = 0x21
    }

    private static readonly Dictionary<FilterTypes, Func<BlockFilter>> FilterMap = new Dictionary<
        FilterTypes,
        Func<BlockFilter>
    >
    {
        { FilterTypes.ARCH_x86_FILTER, () => new X86Filter() },
        { FilterTypes.ARCH_PowerPC_FILTER, () => new PowerPCFilter() },
        { FilterTypes.ARCH_IA64_FILTER, () => new IA64Filter() },
        { FilterTypes.ARCH_ARM_FILTER, () => new ArmFilter() },
        { FilterTypes.ARCH_ARMTHUMB_FILTER, () => new ArmThumbFilter() },
        { FilterTypes.ARCH_SPARC_FILTER, () => new SparcFilter() },
        { FilterTypes.LZMA2, () => new Lzma2Filter() }
    };

    public abstract bool AllowAsLast { get; }
    public abstract bool AllowAsNonLast { get; }
    public abstract bool ChangesDataSize { get; }

    public abstract void Init(byte[] properties);
    public abstract void ValidateFilter();

    public static BlockFilter Read(BinaryReader reader)
    {
        var filterType = (FilterTypes)reader.ReadXZInteger();
        if (!FilterMap.ContainsKey(filterType))
        {
            throw new NotImplementedException($"Filter {filterType} has not yet been implemented");
        }

        var filter = FilterMap[filterType]()!;

        var sizeOfProperties = reader.ReadXZInteger();
        if (sizeOfProperties > int.MaxValue)
        {
            throw new InvalidDataException("Block filter information too large");
        }

        var properties = reader.ReadBytes((int)sizeOfProperties);
        filter.Init(properties);
        return filter;
    }

    public abstract void SetBaseStream(Stream stream);
}
