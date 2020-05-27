using System;
using System.Collections.Generic;
using System.Text;

namespace Sockety.Model
{
    public interface IService
    {
        void UdpReceive(object obj);
    }
}
