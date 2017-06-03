using System;

namespace SharpCompress.Compressor.PPMd
{
    public enum PpmdVersion
    {
        H,
        H7z,
        I1,
    }

    public class PpmdProperties
    {
        public PpmdVersion Version = PpmdVersion.I1;
        public int ModelOrder;
        internal I1.ModelRestorationMethod ModelRestorationMethod;

        private int allocatorSize;
        internal I1.Allocator Allocator;

        public PpmdProperties()
            : this(16 << 20, 6)
        {
        }

        public PpmdProperties(int allocatorSize, int modelOrder)
            : this(allocatorSize, modelOrder, I1.ModelRestorationMethod.Restart)
        {
        }

        internal PpmdProperties(int allocatorSize, int modelOrder, I1.ModelRestorationMethod modelRestorationMethod)
        {
            AllocatorSize = allocatorSize;
            ModelOrder = modelOrder;
            ModelRestorationMethod = modelRestorationMethod;
        }

        public PpmdProperties(byte[] properties)
        {
            if (properties.Length == 2)
            {
                ushort props = BitConverter.ToUInt16(properties, 0);
                AllocatorSize = (((props >> 4) & 0xff) + 1) << 20;
                ModelOrder = (props & 0x0f) + 1;
                ModelRestorationMethod = (I1.ModelRestorationMethod)(props >> 12);
            }
            else if (properties.Length == 5)
            {
                Version = PpmdVersion.H7z;
                AllocatorSize = BitConverter.ToInt32(properties, 1);
                ModelOrder = properties[0];
            }
        }

        public int AllocatorSize
        {
            get
            {
                return allocatorSize;
            }
            set
            {
                allocatorSize = value;
                if (Version == PpmdVersion.I1)
                {
                    if (Allocator == null)
                        Allocator = new I1.Allocator();
                    Allocator.Start(allocatorSize);
                }
            }
        }

        public byte[] Properties
        {
            get
            {
                return BitConverter.GetBytes((ushort)((ModelOrder - 1) + (((AllocatorSize >> 20) - 1) << 4) + ((ushort)ModelRestorationMethod << 12)));
            }
        }
    }
}
