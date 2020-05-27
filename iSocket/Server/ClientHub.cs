using iSocket.Model;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace iSocket.Server
{
    internal class ClientHub : IDisposable
    {
        private Socket handler = null;
        private Thread thread = null;
        internal ClientInfo ClientInfo;

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

       
        internal void SendNonReturn(string ClientMethodName,byte[] data)
        {
            try
            {
                var packet = new ISocketPacket() { MethodName = ClientMethodName, PackData = data };
                var d = MessagePackSerializer.Serialize(packet);
                handler.Send(d);
            }catch(Exception ex)
            {
                throw ex;
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
