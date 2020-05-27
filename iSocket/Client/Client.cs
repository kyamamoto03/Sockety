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
        private IPEndPoint ServerEndPoint;
        private ClientInfo clientInfo;
        public Action ConnectionReset;

        public void Connect(string ServerHost, int PortNumber,string UserName,object parent)
        {
            clientReceiver.ConnectionReset = ConnectionReset;
            Parent = (T)parent;

            IPHostEntry ipHostInfo = Dns.GetHostEntry(ServerHost);
            var host = ipHostInfo.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();
            ServerEndPoint = new IPEndPoint(host, PortNumber);

            // Create a TCP/IP  socket.  
            serverSocket = new Socket(ServerEndPoint.AddressFamily,SocketType.Stream, ProtocolType.Tcp);

            try
            {
                serverSocket.Connect(ServerEndPoint);

                Console.WriteLine("Socket connected to {0}",
                    serverSocket.RemoteEndPoint.ToString());

                //新規の接続なのでClientInfoを作成
                clientInfo = CreateNewClientInfo(UserName);
                //接続出来たらクライアント情報を送る
                SendClientInfo(serverSocket, clientInfo);

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

        public bool ReConnect()
        {
            if (ServerEndPoint == null)
            {
                //一度も接続していない
                throw new Exception("Not Connect");
            }

            if (serverSocket.Connected == false)
            {
                try
                {
                    serverSocket.Close();
                    serverSocket = new Socket(ServerEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    //接続処理
                    serverSocket.Connect(ServerEndPoint);
                    SendClientInfo(serverSocket, clientInfo);
                    clientReceiver.Run(serverSocket, Parent);
                }catch(SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionRefused)
                    {
                        //いまだ接続できず
                        return false;
                    }
                    throw ex;
                }
                catch(Exception ex)
                {
                    throw ex;
                }
            }
            return true;
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
                clientReceiver.AbortReceiveProcess();
                serverSocket.Shutdown(SocketShutdown.Both);
                serverSocket.Close();
                serverSocket = null;
            }
        }

        private ClientInfo CreateNewClientInfo(string UserName)
        {
            ClientInfo clientInfo = new ClientInfo();
            clientInfo.ClientID = Guid.NewGuid();
            clientInfo.Name = UserName;

            return clientInfo;
        }

        private void SendClientInfo(Socket socket, ClientInfo clientInfo)
        {
            byte[] bytes = MessagePackSerializer.Serialize(clientInfo);
            socket.Send(bytes);
        }
    }
}
