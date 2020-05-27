using System;
using System.Collections.Generic;
using System.Text;

namespace iSocket.Server
{
    internal class ISocketClient<T>
    {
        private static ISocketClient<T> _instance { get; set; }
        public List<ClientHub<T>> ClientHubs = new List<ClientHub<T>>();

        public static ISocketClient<T> GetInstance()
        {
            return _instance = _instance ?? new ISocketClient<T>();
        }

    }
}
