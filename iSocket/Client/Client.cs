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

        private byte[] CommunicateButter = new byte[1024];

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
                SendClientInfo(serverSocket);

                while (true)
                {
                    // Encode the data string into a byte array.  
                    byte[] msg = Encoding.ASCII.GetBytes(DateTime.Now.ToString());

                    // Send the data through the socket.  
                    int bytesSent = serverSocket.Send(msg);

                    // Receive the response from the remote device.  
                    int bytesRec = serverSocket.Receive(CommunicateButter);
                    Console.WriteLine("{0}",
                        Encoding.ASCII.GetString(CommunicateButter, 0, bytesRec));

                    System.Threading.Thread.Sleep(2000);
                }

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

            byte[] bytes = MessagePackSerializer.Serialize(clientInfo);
            socket.Send(bytes);
        }
    }
}
