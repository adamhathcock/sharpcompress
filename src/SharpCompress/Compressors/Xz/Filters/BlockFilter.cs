using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Compressors.Xz.Filters
{
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

        private static readonly Dictionary<FilterTypes, Type> FilterMap = new Dictionary<
            FilterTypes,
            Type
        >
        {
            { FilterTypes.ARCH_x86_FILTER, typeof(X86Filter) },
            { FilterTypes.ARCH_PowerPC_FILTER, typeof(PowerPCFilter) },
            { FilterTypes.ARCH_IA64_FILTER, typeof(IA64Filter) },
            { FilterTypes.ARCH_ARM_FILTER, typeof(ArmFilter) },
            { FilterTypes.ARCH_ARMTHUMB_FILTER, typeof(ArmThumbFilter) },
            { FilterTypes.ARCH_SPARC_FILTER, typeof(SparcFilter) },
            { FilterTypes.LZMA2, typeof(Lzma2Filter) }
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
                throw new NotImplementedException(
                    $"Filter {filterType} has not yet been implemented"
                );
            }

            var filter = (BlockFilter)Activator.CreateInstance(FilterMap[filterType])!;

            var sizeOfProperties = reader.ReadXZInteger();
            if (sizeOfProperties > int.MaxValue)
            {
                throw new InvalidDataException("Block filter information too large");
            }

            byte[] properties = reader.ReadBytes((int)sizeOfProperties);
            filter.Init(properties);
            return filter;
        }

        public abstract void SetBaseStream(Stream stream);
    }
}
