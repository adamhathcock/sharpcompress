namespace SharpCompress.Common.Rar.Headers
{
    using System;
    using System.Text;

    internal static class FileNameDecoder
    {
        internal static string Decode(byte[] name, int encPos)
        {
            int pos = 0;
            int num2 = 0;
            int num3 = 0;
            int num4 = 0;
            int num5 = 0;
            int num6 = GetChar(name, encPos++);
            StringBuilder builder = new StringBuilder();
            while (encPos < name.Length)
            {
                int num7;
                int num8;
                if (num3 == 0)
                {
                    num2 = GetChar(name, encPos++);
                    num3 = 8;
                }
                switch ((num2 >> 6))
                {
                    case 0:
                        builder.Append((char) GetChar(name, encPos++));
                        pos++;
                        goto Label_0191;

                    case 1:
                        builder.Append((char) (GetChar(name, encPos++) + (num6 << 8)));
                        pos++;
                        goto Label_0191;

                    case 2:
                        num4 = GetChar(name, encPos);
                        num5 = GetChar(name, encPos + 1);
                        builder.Append((char) ((num5 << 8) + num4));
                        pos++;
                        encPos += 2;
                        goto Label_0191;

                    case 3:
                        num7 = GetChar(name, encPos++);
                        if ((num7 & 0x80) == 0)
                        {
                            goto Label_0154;
                        }
                        num8 = GetChar(name, encPos++);
                        num7 = (num7 & 0x7f) + 2;
                        goto Label_013C;

                    default:
                        goto Label_0191;
                }
            Label_0110:
                num4 = (GetChar(name, pos) + num8) & 0xff;
                builder.Append((char) ((num6 << 8) + num4));
                num7--;
                pos++;
            Label_013C:
                if ((num7 > 0) && (pos < name.Length))
                {
                    goto Label_0110;
                }
                goto Label_0191;
            Label_0154:
                num7 += 2;
                while ((num7 > 0) && (pos < name.Length))
                {
                    builder.Append((char) GetChar(name, pos));
                    num7--;
                    pos++;
                }
            Label_0191:
                num2 = (num2 << 2) & 0xff;
                num3 -= 2;
            }
            return builder.ToString();
        }

        internal static int GetChar(byte[] name, int pos)
        {
            return (name[pos] & 0xff);
        }
    }
}

