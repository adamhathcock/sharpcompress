using System.Collections.Generic;

namespace SharpCompress.Compressors.Rar.VM;

internal class VMPreparedProgram
{
  internal List<VMPreparedCommand> Commands = [];
  internal List<VMPreparedCommand> AltCommands = [];

  public int CommandCount { get; set; }

  internal List<byte> GlobalData = [];
  internal List<byte> StaticData = [];

  // static data contained in DB operators
  internal int[] InitR = new int[7];

  internal int FilteredDataOffset { get; set; }
  internal int FilteredDataSize { get; set; }
}
