using Sockety.Model;
using System.Collections.Generic;

namespace Sockety.Server
{
    internal class SocketClient<T> where T : IService
    {
        private static SocketClient<T> _instance { get; set; }
        private List<ClientHub<T>> _ClientHubs = new List<ClientHub<T>>();

        public List<ClientHub<T>> ClientHubs { get => _ClientHubs; }

        public static SocketClient<T> GetInstance()
        {
            return _instance = _instance ?? new SocketClient<T>();
        }

        public void AddClientHub(ClientHub<T> c)
        {
            lock (_ClientHubs)
            {
                _ClientHubs.Add(c);
            }
        }
        public void RemoveClientHub(ClientHub<T> c)
        {
            lock(_ClientHubs)
            {
                _ClientHubs.Remove(c);
            }
        }
    }
}
