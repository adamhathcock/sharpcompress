using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Rar.UnpackV1
{
    internal partial class Unpack
    {
        uint SlotToLength(uint Slot)
        {
          //uint LBits,Length=2;
          int LBits;
          uint Length=2;
          if (Slot<8)
          {
            LBits=0;
            Length+=Slot;
          }
          else
          {
            //LBits=Slot/4-1;
            LBits=(int)(Slot/4-1);
            Length+=(4 | (Slot & 3)) << LBits;
          }

          if (LBits>0)
          {
            Length+=getbits()>>(16-LBits);
            AddBits(LBits);
          }
          return Length;
        }
    }
}
