using System;
using System.Collections.Generic;
using System.Text;

namespace Sockety.Model
{
    public class PacketSize
    {
        public byte[] Header { get; set; } = new byte[4];
        public int Size { get; set; }
        public static PacketSize Create(int size)
        {
            PacketSize ret = new PacketSize();
            ret.Header[0] = 0x01;
            ret.Header[1] = 0x02;
            ret.Header[2] = 0x03;
            ret.Header[3] = 0x04;

            ret.Size = size;
            return ret;
        }

        private bool Validation(PacketSize p)
        {
            if (p.Header[0] == 0x01 && 
                p.Header[1] == 0x02 && 
                p.Header[2] == 0x03 && 
                p.Header[3] == 0x04)
            {
                return true;
            }
            return false;
        }
        public static PacketSize FromBytes(byte[]data)
        {
            PacketSize ret = new PacketSize();

            if (data.Length != 8)
            {
                return null;
            }
            for(int i = 0;i < ret.Header.Length;i++)
            {
                ret.Header[i] = data[i];
            }
            if (ret.Validation(ret) == false)
            {
                return null;
            }


            ret.Size = BitConverter.ToInt32(data, 4);

            return ret;
        }

        public byte[] GetBytes()
        {
            byte[] b = new byte[8];
            b[0] = Header[0];
            b[1] = Header[1];
            b[2] = Header[2];
            b[3] = Header[3];

            var a = BitConverter.GetBytes(Size);
            b[4] = a[0];
            b[5] = a[1];
            b[6] = a[2];
            b[7] = a[3];
            return b;
        }
    }
}
