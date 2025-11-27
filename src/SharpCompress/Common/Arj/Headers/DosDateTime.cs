using System;

namespace SharpCompress.Common.Arj.Headers
{
    public class DosDateTime
    {
        public DateTime DateTime { get; }

        public DosDateTime(long dosValue)
        {
            // Ensure only the lower 32 bits are used
            int value = unchecked((int)(dosValue & 0xFFFFFFFF));

            var date = (value >> 16) & 0xFFFF;
            var time = value & 0xFFFF;

            var day = date & 0x1F;
            var month = (date >> 5) & 0x0F;
            var year = ((date >> 9) & 0x7F) + 1980;

            var second = (time & 0x1F) * 2;
            var minute = (time >> 5) & 0x3F;
            var hour = (time >> 11) & 0x1F;

            try
            {
                DateTime = new DateTime(year, month, day, hour, minute, second);
            }
            catch
            {
                DateTime = DateTime.MinValue;
            }
        }

        public override string ToString() => DateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
