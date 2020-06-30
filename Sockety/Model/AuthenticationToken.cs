using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sockety.Model
{
    [MessagePackObject]
    public class AuthenticationToken
    {
        [Key(1)]
        public string Toekn { get; set; }

    }
}
