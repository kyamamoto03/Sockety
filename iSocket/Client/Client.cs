using iSocket.Model;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace iSocket.Client
{
    public class Client : IDisposable
    {
        private Socket serverSocket;
        public ClientReceiver clientReceiver { get; } = new ClientReceiver();

        public void Connect(string ServerHost, int PortNumber)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(ServerHost);
            var host = ipHostInfo.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();
            IPEndPoint remoteEP = new IPEndPoint(host, PortNumber);

            // Create a TCP/IP  socket.  
            Socket serverSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            try
            {
                serverSocket.Connect(remoteEP);

                Console.WriteLine("Socket connected to {0}",
                    serverSocket.RemoteEndPoint.ToString());
                //接続出来たらクライアント情報を送る
                SendClientInfo(serverSocket);

                clientReceiver.Run(serverSocket);

            }
            catch (ArgumentNullException ane)
            {
                Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
            }
            catch (SocketException se)
            {
                Console.WriteLine("SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception : {0}", e.ToString());
            }

        }

        #region IDisposable
        public void Dispose()
        {
            DisConnect();
        }
        #endregion

        public void DisConnect()
        {
            if (serverSocket != null)
            {
                serverSocket.Shutdown(SocketShutdown.Both);
                serverSocket.Close();
                serverSocket = null;
            }
        }

        private void SendClientInfo(Socket socket)
        {
            ClientInfo clientInfo = new ClientInfo();
            clientInfo.ClientID = Guid.NewGuid();
            clientInfo.Name = "テスト";

            byte[] bytes = MessagePackSerializer.Serialize(clientInfo);
            socket.Send(bytes);
        }
    }
}
