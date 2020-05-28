using MessagePack;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace Sockety.Model
{
    [MessagePackObject]
    public class SocketyPacket
    {
        [Key(0)]
        public string MethodName { get; set; }
        [Key(1)]
        public ClientInfo clientInfo { get; set; }
        [Key(2)]
        public object PackData { get; set; }
    }
}
