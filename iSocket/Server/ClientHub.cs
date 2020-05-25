using iSocket.Model;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace iSocket.Server
{
    internal class ClientHub : IDisposable
    {
        private Socket handler = null;
        private Thread thread = null;
        private ClientInfo ClientInfo;

        public ClientHub(Socket _handler, ClientInfo _clientInfo)
        {
            this.handler = _handler;
            this.ClientInfo = _clientInfo;
        }

        public void Dispose()
        {
            if (handler != null)
            {
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
                handler = null;
            }
        }

       
        public void Run()
        {
            thread = new Thread(new ThreadStart(Process));
            thread.Start();
        }

        private void Process()
        {
            byte[] bytes = new Byte[1024];

            while (true)
            {
                try
                {
                    string data = null;
                    int bytesRec = handler.Receive(bytes);
                    data = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                    byte[] msg = Encoding.ASCII.GetBytes($"Echo: {data}");
                    handler.Send(msg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    break;
                }
            }

        }

    }
}
