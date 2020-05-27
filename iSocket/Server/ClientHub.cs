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
    internal class ClientHub<T> : IDisposable
    {
        private Socket serverSocket = null;
        private Thread thread = null;
        internal ClientInfo ClientInfo;
        private T Parent;

        public ClientHub(Socket _handler, ClientInfo _clientInfo,T parent)
        {
            this.Parent = parent;
            this.serverSocket = _handler;
            this.ClientInfo = _clientInfo;
        }

        public void Dispose()
        {
            if (serverSocket != null)
            {
                serverSocket.Shutdown(SocketShutdown.Both);
                serverSocket.Close();
                serverSocket = null;
            }
        }

       
        internal void SendNonReturn(string ClientMethodName,byte[] data)
        {
            try
            {
                var packet = new ISocketPacket() { MethodName = ClientMethodName, PackData = data };
                var d = MessagePackSerializer.Serialize(packet);
                serverSocket.Send(d);
            }catch(Exception ex)
            {
                throw ex;
            }
        }

        public void Run()
        {
            thread = new Thread(new ThreadStart(ReceiveProcess));
            thread.Start();

        }

        /// <summary>
        /// 受信を一括して行う
        /// </summary>
        private async void ReceiveProcess()
        {
            byte[] CommunicateButter = new byte[1024];
            while (true)
            {
                try
                {
                    int bytesRec = serverSocket.Receive(CommunicateButter);
                    var packet = MessagePackSerializer.Deserialize<ISocketPacket>(CommunicateButter);

                    //メソッドの戻り値を詰め替える
                    packet.PackData = await InvokeMethodAsync(packet);
                    //送り返す
                    serverSocket.Send(MessagePackSerializer.Serialize(packet));
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        //通信切断
                        //Task.Run(() => ConnectionReset?.Invoke());

                        //受信スレッド終了
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                Thread.Sleep(10);
            }
        }

        private async Task<byte[]> InvokeMethodAsync(ISocketPacket packet)
        {
            Type t = Parent.GetType();
            var method = t.GetMethod(packet.MethodName);

            if (method == null)
            {
                throw new Exception("not found Method");
            }
            byte[] ret = (byte[])await Task.Run(() => method.Invoke(Parent, new object[] { packet.PackData }));

            return ret;
        }

    }
}
