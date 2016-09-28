namespace SharpCompress.Common
{
    public class OptionsBase
    {
        /// <summary>
        /// SharpCompress will keep the supplied streams open.  Default is true.
        /// </summary>
        public bool LeaveOpenStream { get; set; } = true;
    }
}