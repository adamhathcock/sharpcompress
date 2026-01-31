using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SharpCompress.IO
{
    public interface IStreamStack
    {

        /// <summary>
        /// Returns the immediate underlying stream in the stack.
        /// </summary>
        Stream BaseStream();

    }
}
