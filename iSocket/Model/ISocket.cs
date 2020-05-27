using System;
using System.Collections.Generic;
using System.Text;

namespace iSocket.Model
{
    public interface ISocket
    {
        void UdpReceive(object obj);
    }
}
