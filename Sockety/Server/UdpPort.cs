using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Sockety.Server
{
    public class UdpPort<T>
    {
        public bool IsConnect { get; set; }
        public int UdpPortNumber { get; set; }
        public Socket PunchingSocket { get; set; }
        public IPEndPoint PunchingPoint { get; set; }
    }

    public class UserCommunicateService<T>
    {
        private static List<UdpPort<T>> _UdpPorts;
        private static int PORT_START = 11000;
        private static int PORT_END = 11020;

        public static List<UdpPort<T>> Get()
        {
            if (_UdpPorts == null)
            {
                _UdpPorts = new List<UdpPort<T>>();
                for (int i = PORT_START; i <= PORT_END; i++)
                {
                    _UdpPorts.Add(new UdpPort<T>() { UdpPortNumber = i });
                }

            }
            return _UdpPorts;
        }
    }

}
