using System;
using System.Collections.Generic;
using System.Text;

namespace Share
{
    public interface IClient : ISocketClient
    {
        void UserJoin(byte[] packet);
        void Push(byte[] a);
    }

    public interface ISocketClient
    {

    }
}
