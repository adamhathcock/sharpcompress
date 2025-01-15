using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Compressors.Xz.Filters;

public abstract class BlockFilter : ReadOnlyStream
{
    private enum FilterTypes : ulong
    {
        //Delta = 0x03,
        ArchX86Filter = 0x04,
        ArchPowerPcFilter = 0x05,
        ArchIa64Filter = 0x06,
        ArchArmFilter = 0x07,
        ArchArmThumbFilter = 0x08,
        ArchSparcFilter = 0x09,
        Lzma2 = 0x21,
    }

    private static readonly Dictionary<FilterTypes, Func<BlockFilter>> FILTER_MAP = new()
    {
        { FilterTypes.ArchX86Filter, () => new X86Filter() },
        { FilterTypes.ArchPowerPcFilter, () => new PowerPCFilter() },
        { FilterTypes.ArchIa64Filter, () => new IA64Filter() },
        { FilterTypes.ArchArmFilter, () => new ArmFilter() },
        { FilterTypes.ArchArmThumbFilter, () => new ArmThumbFilter() },
        { FilterTypes.ArchSparcFilter, () => new SparcFilter() },
        { FilterTypes.Lzma2, () => new Lzma2Filter() },
    };

    public abstract bool AllowAsLast { get; }
    public abstract bool AllowAsNonLast { get; }
    public abstract bool ChangesDataSize { get; }

    public abstract void Init(byte[] properties);
    public abstract void ValidateFilter();

    public static BlockFilter Read(BinaryReader reader)
    {
        var filterType = (FilterTypes)reader.ReadXZInteger();
        if (!FILTER_MAP.ContainsKey(filterType))
        {
            throw new NotImplementedException($"Filter {filterType} has not yet been implemented");
        }

        var filter = FILTER_MAP[filterType]();

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
