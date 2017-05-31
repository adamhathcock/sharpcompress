using System;

namespace SharpCompress.Common.Tar
{
	/// <summary>
	/// TarException represents exceptions specific to Tar classes and code.
	/// </summary>
	public class TarException : ArchiveException
	{
		/// <summary>
		/// Initialise a new instance of <see cref="TarException" /> with its message string.
		/// </summary>
		/// <param name="message">A <see cref="string"/> that describes the error.</param>
		public TarException(string message)
			: base(message)
		{
		}
	}
}
