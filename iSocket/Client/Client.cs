using iSocket.Model;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text;

namespace iSocket.Client
{
    public class Client<T> : IDisposable
    {
        private Socket serverSocket;
        public ClientReceiver<T> clientReceiver { get; } = new ClientReceiver<T>();
        private T Parent;

        public void Connect(string ServerHost, int PortNumber,string UserName,object parent)
        {
            Parent = (T)parent;

            //IPHostEntry ipHostInfo = Dns.GetHostEntry(ServerHost);
            //var host = ipHostInfo.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();
            IPAddress ipAddress = IPAddress.Parse("192.168.2.12");
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, PortNumber);

            // Create a TCP/IP  socket.  
            serverSocket = new Socket(ipAddress.AddressFamily,SocketType.Stream, ProtocolType.Tcp);

            try
            {
                serverSocket.Connect(remoteEP);

                Console.WriteLine("Socket connected to {0}",
                    serverSocket.RemoteEndPoint.ToString());
                //接続出来たらクライアント情報を送る
                SendClientInfo(serverSocket, UserName);

                clientReceiver.Run(serverSocket, Parent);

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

        private void SendClientInfo(Socket socket,string UserName)
        {
            ClientInfo clientInfo = new ClientInfo();
            clientInfo.ClientID = Guid.NewGuid();
            clientInfo.Name = UserName;

            byte[] bytes = MessagePackSerializer.Serialize(clientInfo);
            socket.Send(bytes);
        }
    }
}
