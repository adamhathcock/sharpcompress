#region Using

using System;

#endregion

namespace SharpCompress.Compressor.PPMd.I1
{
    /// <summary>
    /// SEE2 (secondary escape estimation) contexts for PPM contexts with masked symbols.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This must be a class rather than a structure because MakeEscapeFrequency returns a See2Context
    /// instance from the see2Contexts array.  The caller (for example, EncodeSymbol2) then updates the
    /// returned See2Context instance and expects the updates to be reflected in the see2Contexts array.
    /// This would not happen if this were a structure.
    /// </para>
    /// <remarks>
    /// Note that in most cases fields are used rather than properties for performance reasons (for example,
    /// <see cref="Shift"/> is a field rather than a property).
    /// </remarks>
    /// </remarks>
    internal class See2Context
    {
        private const byte PeriodBitCount = 7;

        public ushort Summary;
        public byte Shift;
        public byte Count;

        public void Initialize(uint initialValue)
        {
            Shift = PeriodBitCount - 4;
            Summary = (ushort) (initialValue << Shift);
            Count = 7;
        }

        public uint Mean()
        {
            uint value = (uint) (Summary >> Shift);
            Summary = (ushort) (Summary - value);
            return (uint) (value + ((value == 0) ? 1 : 0));
        }

        public void Update()
        {
            if (Shift < PeriodBitCount && --Count == 0)
            {
                Summary += Summary;
                Count = (byte) (3 << Shift++);
            }
        }
    }
}
