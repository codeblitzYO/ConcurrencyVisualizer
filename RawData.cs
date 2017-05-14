using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETW
{
    public static class RawData
    {
        static public int getUnicodeBytes(byte[] bytes, int start)
        {
            int i;
            for (i = start; i < bytes.Length; i += 2)
            {
                var c = BitConverter.ToInt16(bytes, i);
                if (c == '\0') break;
            }
            return i - start;
        }

        static public int getMulticharBytes(byte[] bytes, int start)
        {
            int i;
            for (i = start; i < bytes.Length; ++i)
            {
                var c = bytes[i];
                if (c == '\0') break;
            }
            return i - start;
        }

    }
}
