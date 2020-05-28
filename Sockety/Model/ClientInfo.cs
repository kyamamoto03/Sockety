using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace Sockety.Model
{
    [MessagePackObject]
    public class ClientInfo
    {
        [Key(0)]
        public Guid ClientID { get; set; }
        [Key(1)]
        public string Name { get;  set; }
        [Key(2)]
        public DateTime ConnectDate { get; private set; } = DateTime.Now;

        public override bool Equals(object obj)
        {
            ClientInfo c = (ClientInfo)obj;
            if (this.ClientID == c.ClientID)
            {
                return true;
            }
            return false;
        }
    }
}
