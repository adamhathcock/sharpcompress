using SharpCompress.Compressors.PPMd.I1;
using SharpCompress.Converters;

namespace SharpCompress.Compressors.PPMd
{
    public class PpmdProperties
    {
        public PpmdVersion Version = PpmdVersion.I1;
        public int ModelOrder;
        internal ModelRestorationMethod ModelRestorationMethod;

        private int allocatorSize;
        internal Allocator Allocator;

        public PpmdProperties()
            : this(16 << 20, 6)
        {
        }

        public PpmdProperties(int allocatorSize, int modelOrder)
            : this(allocatorSize, modelOrder, ModelRestorationMethod.Restart)
        {
        }

        internal PpmdProperties(int allocatorSize, int modelOrder, ModelRestorationMethod modelRestorationMethod)
        {
            AllocatorSize = allocatorSize;
            ModelOrder = modelOrder;
            ModelRestorationMethod = modelRestorationMethod;
        }

        public PpmdProperties(byte[] properties)
        {
            if (properties.Length == 2)
            {
                ushort props = DataConverter.LittleEndian.GetUInt16(properties, 0);
                AllocatorSize = (((props >> 4) & 0xff) + 1) << 20;
                ModelOrder = (props & 0x0f) + 1;
                ModelRestorationMethod = (ModelRestorationMethod)(props >> 12);
            }
            else if (properties.Length == 5)
            {
                Version = PpmdVersion.H7z;
                AllocatorSize = DataConverter.LittleEndian.GetInt32(properties, 1);
                ModelOrder = properties[0];
            }
        }

        public int AllocatorSize
        {
            get => allocatorSize;
            set
            {
                allocatorSize = value;
                if (Version == PpmdVersion.I1)
                {
                    if (Allocator == null)
                    {
                        Allocator = new Allocator();
                    }
                    Allocator.Start(allocatorSize);
                }
            }
        }

        public byte[] Properties => DataConverter.LittleEndian.GetBytes(
                                                                        (ushort)
                                                                        ((ModelOrder - 1) + (((AllocatorSize >> 20) - 1) << 4) + ((ushort)ModelRestorationMethod << 12)));
    }
}