using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Common.Rar;
internal interface ICryptKey
{
    ICryptoTransform Transformer(byte[] salt);
}
