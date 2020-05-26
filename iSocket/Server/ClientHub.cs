using iSocket.Model;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

            Task.Run(() =>
            {
                int cnt = 0;
                while (true)
                {

                    if (cnt++ == 5)
                    {
                        var packet = new ISocketPacket();
                        packet.MethodName = "Push";
                        handler.Send(MessagePackSerializer.Serialize(packet));
                        cnt = 0;
                    }
                    System.Threading.Thread.Sleep(1200);   
                }

            });
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
                    {
                        var packet = new ISocketPacket();
                        packet.MethodName = "Echo";
                        packet.PackData = Encoding.ASCII.GetBytes($"Echo: {data}");
                        handler.Send(MessagePackSerializer.Serialize(packet));
                    }
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
