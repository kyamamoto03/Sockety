using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sockety.Model
{
    /// <summary>
    /// サーバの接続情報をクライアントに送信するときに使用
    /// </summary>
    [MessagePackObject]
    public class ConnectionSetting
    {
        [Key(0)]
        public bool UseSSL { get; set; }
    }
}
