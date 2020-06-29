using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sockety.Model
{
    [MessagePackObject]
    public class ConnectionSetting
    {
        [Key(0)]
        public bool UseSSL { get; set; }
    }
}
