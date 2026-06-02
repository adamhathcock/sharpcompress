using System.Collections.Generic;

namespace SharpCompress.Compressors.Rar.VM;

internal class VMPreparedProgram
{
    internal List<VMPreparedCommand> Commands = new(16);
    internal List<VMPreparedCommand> AltCommands = new(16);

    public int CommandCount { get; set; }

    internal List<byte> GlobalData = new(RarVM.VM_FIXEDGLOBALSIZE);
    internal List<byte> StaticData = new();

    // static data contained in DB operators
    internal int[] InitR = new int[7];

    internal int FilteredDataOffset { get; set; }
    internal int FilteredDataSize { get; set; }
}
