namespace SharpCompress.Compressor.PPMd
{
    using SharpCompress.Compressor.PPMd.I1;
    using System;

    public class PpmdProperties
    {
        internal SharpCompress.Compressor.PPMd.I1.Allocator Allocator;
        private int allocatorSize;
        public int ModelOrder;
        internal SharpCompress.Compressor.PPMd.I1.ModelRestorationMethod ModelRestorationMethod;
        public PpmdVersion Version;

        public PpmdProperties() : this(0x1000000, 6)
        {
        }

        public PpmdProperties(byte[] properties)
        {
            this.Version = PpmdVersion.I1;
            if (properties.Length == 2)
            {
                ushort num = BitConverter.ToUInt16(properties, 0);
                this.AllocatorSize = (((num >> 4) & 0xff) + 1) << 20;
                this.ModelOrder = (num & 15) + 1;
                this.ModelRestorationMethod = (SharpCompress.Compressor.PPMd.I1.ModelRestorationMethod) (num >> 12);
            }
            else if (properties.Length == 5)
            {
                this.Version = PpmdVersion.H7z;
                this.AllocatorSize = BitConverter.ToInt32(properties, 1);
                this.ModelOrder = properties[0];
            }
        }

        public PpmdProperties(int allocatorSize, int modelOrder) : this(allocatorSize, modelOrder, SharpCompress.Compressor.PPMd.I1.ModelRestorationMethod.Restart)
        {
        }

        internal PpmdProperties(int allocatorSize, int modelOrder, SharpCompress.Compressor.PPMd.I1.ModelRestorationMethod modelRestorationMethod)
        {
            this.Version = PpmdVersion.I1;
            this.AllocatorSize = allocatorSize;
            this.ModelOrder = modelOrder;
            this.ModelRestorationMethod = modelRestorationMethod;
        }

        public int AllocatorSize
        {
            get
            {
                return this.allocatorSize;
            }
            set
            {
                this.allocatorSize = value;
                if (this.Version == PpmdVersion.I1)
                {
                    if (this.Allocator == null)
                    {
                        this.Allocator = new SharpCompress.Compressor.PPMd.I1.Allocator();
                    }
                    this.Allocator.Start(this.allocatorSize);
                }
            }
        }

        public byte[] Properties
        {
            get
            {
                return BitConverter.GetBytes((ushort) (((this.ModelOrder - 1) + (((this.AllocatorSize >> 20) - 1) << 4)) + (((ushort) this.ModelRestorationMethod) << 12)));
            }
        }
    }
}

