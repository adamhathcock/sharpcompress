/*
 * X86Filter.cs -- XZ converter x86 executable
 * <Contribution by Louis-Michel Bergeron, on behalf of aDolus Technolog Inc., 2022>
 * @TODO Properties offset
 */

using System.IO;
using SharpCompress.Compressors.Filters;

namespace SharpCompress.Compressors.Xz.Filters;

public class X86Filter : BlockFilter
{
    public override bool AllowAsLast => false;

    public override bool AllowAsNonLast => true;

    public override bool ChangesDataSize => false;

    private uint _ip = 0;

    private uint _state = 0;

    //private UInt32 _offset = 0;

    public override void Init(byte[] properties)
    {
        if (properties.Length != 0 && properties.Length != 4)
        {
            throw new InvalidDataException("X86 properties unexpected length");
        }

        if (properties.Length == 4)
        {
            // Even XZ doesn't support it.
            throw new InvalidDataException("X86 properties offset is not supported");

            //_offset = BitConverter.ToUInt32(properties, 0);
            //
            //if (_offset % (UInt32)BranchExec.Alignment.ARCH_x86_ALIGNMENT != 0)
            //{
            //    throw new InvalidDataException("Filter offset does not match alignment");
            //}
        }
    }

    public override void ValidateFilter() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = BaseStream.Read(buffer, offset, count);
        BranchExecFilter.X86Converter(buffer, _ip, ref _state);
        _ip += (uint)bytesRead;
        return bytesRead;
    }

    public override void SetBaseStream(Stream stream) => BaseStream = stream;
}
