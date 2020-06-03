using MessagePack;
using System;

namespace Sockety.Model
{
    [MessagePackObject]
    public class SocketyPacketUDP
    {
        [Key(0)]
        public string MethodName { get; set; }
        [Key(1)]
        public ClientInfo clientInfo { get; set; }
        [Key(2)]
        public byte[] PackData { get; set; }
        [Key(3)]
        public Guid PacketID { get; set; }
        /// <summary>
        ///パケットの総数
        /// <summary>
        [Key(4)]
        public int PacketCount { get; set; }
        /// <summary>
        /// パケットの番号
        /// </summary>
        [Key(5)]
        public int PacketNo { get; set; }
    }
}
