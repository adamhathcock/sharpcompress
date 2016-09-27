using SharpCompress.Common;

namespace SharpCompress.Readers
{
    public class ReaderOptions
    {
        /// <summary>
        /// SharpCompress will keep the supplied streams open.  Default is true.
        /// </summary>
        public virtual bool LeaveOpenStream { get; set; } = true;

        /// <summary>
        /// Look for RarArchive (Check for self-extracting archives or cases where RarArchive isn't at the start of the file)
        /// </summary>
        public bool LookForHeader { get; set; }
        public string Password { get; set; }
    }
}