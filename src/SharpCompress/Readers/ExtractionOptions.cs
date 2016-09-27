namespace SharpCompress.Readers
{
    public class ExtractionOptions
    {
        /// <summary>
        /// overwrite target if it exists
        /// </summary>
        public bool Overwrite  {get; set; }

        /// <summary>
        /// extract with internal directory structure
        /// </summary>
        public bool ExtractFullPath { get; set; }

        /// <summary>
        /// preserve file time
        /// </summary>
        public bool PreserveFileTime { get; set; }

        /// <summary>
        /// preserve windows file attributes
        /// </summary>
        public bool PreserveAttributes { get; set; }
    }
}