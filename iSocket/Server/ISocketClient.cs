using System;
using System.Collections.Generic;
using System.Text;

namespace iSocket.Server
{
    internal class ISocketClient
    {
        private static ISocketClient _instance { get; set; }
        public List<ClientHub> ClientHubs = new List<ClientHub>();

        public static ISocketClient GetInstance()
        {
            return _instance = _instance ?? new ISocketClient();
        }

    }
}
