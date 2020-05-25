using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace iSocket.Model
{
    [MessagePackObject]
    public class ClientInfo
    {
        [Key(0)]
        public Guid ClientID { get; set; }
        [Key(1)]
        public string Name { get; private set; }
        [Key(2)]
        public DateTime ConnectDate { get; private set; }
    }
}
