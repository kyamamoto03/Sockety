using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public static List<UdpPort<T>> Get()
        {
            if (_UdpPorts == null)
            {
                throw new System.Exception("UserCommunicateService now Init");
            }
            return _UdpPorts;
        }
        public static void Init(int StartPort,int EndPort)
        {
            if (StartPort > EndPort)
            {
                throw new Exception("StartPortとEndPortが逆転しています");
            }
            if (ValidPort(StartPort) == false)
            {
                throw new Exception("Invalid StartPort");
            }
            if (ValidPort(EndPort) == false)
            {
                throw new Exception("Invalid EndPort");
            }

            _UdpPorts = new List<UdpPort<T>>();
            for (int i = StartPort; i <= EndPort; i++)
            {
                _UdpPorts.Add(new UdpPort<T>() { UdpPortNumber = i });
            }

        }

        private static  bool ValidPort(int port)
        {
            if (port < 1024)
            {
                return false;
            }
            if (port > UInt16.MaxValue)
            {
                return false;
            }

            return true;
        }
    }

}
