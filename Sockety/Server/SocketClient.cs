using Sockety.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sockety.Server
{
    internal class SocketClient<T> where T : IService
    {
        private static SocketClient<T> _instance { get; set; }
        public List<ClientHub<T>> ClientHubs = new List<ClientHub<T>>();

        public static SocketClient<T> GetInstance()
        {
            return _instance = _instance ?? new SocketClient<T>();
        }

    }
}
