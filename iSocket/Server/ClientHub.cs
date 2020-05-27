using iSocket.Client;
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
    public class ClientHub<T> : IDisposable
    {
        private Socket serverSocket = null;
        private Thread TcpThread;
        private Thread UdpThread;
        public ClientInfo ClientInfo;
        private T Parent;
        CancellationTokenSource stoppingCts;
        private UdpPort<T> UdpPort;
        /// <summary>
        /// クライアントが切断時に発火
        /// </summary>
        public Action<ClientInfo> ConnectionReset;


        public ClientHub(Socket _handler, ClientInfo _clientInfo, UdpPort<T> udpPort, CancellationTokenSource _stoppingCts,T parent)
        {
            this.Parent = parent;
            this.serverSocket = _handler;
            this.ClientInfo = _clientInfo;
            this.UdpPort = udpPort;
            this.stoppingCts = _stoppingCts;
        }

        public void Dispose()
        {
            if (serverSocket != null)
            {
                TcpThread.Abort();
                UdpThread.Abort();

                serverSocket.Shutdown(SocketShutdown.Both);
                serverSocket.Close();
                serverSocket = null;
            }
        }

       
        internal void SendNonReturn(string ClientMethodName,object data)
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

        internal void SendUdp(object data)
        {
            try
            {
                var packet = new ISocketPacket() { MethodName = "UdpReceive", PackData = data };
                var bytes = MessagePackSerializer.Serialize(packet);
                UdpPort.PunchingSocket.SendTo(bytes, SocketFlags.None, UdpPort.PunchingPoint);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Run()
        {
            TcpThread = new Thread(new ThreadStart(ReceiveProcess));
            TcpThread.Start();

            UdpThread = new Thread(new ThreadStart(UdpReceiveProcess));
            UdpThread.Start();
        }

        private void UdpReceiveProcess()
        {
            byte[] CommunicateButter = new byte[1024];
            UdpPort.PunchingSocket.ReceiveTimeout = 5000;
            while (true)
            {
                try
                {
                    int cnt = UdpPort.PunchingSocket.Receive(CommunicateButter);
                    var str = MessagePackSerializer.Deserialize<string>(CommunicateButter);

                    Console.WriteLine($"UDP:{str}");
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.TimedOut && stoppingCts.IsCancellationRequested == true)
                    {
                        //終了フラグが立ってるので、スレッドを終了する
                        return;
                    }
                }catch(Exception ex)
                {
                    throw ex;
                }
            }
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
                        //クライアント一覧から削除
                        ISocketClient<T>.GetInstance().ClientHubs.Remove(this);
                        //通信切断
                        await Task.Run(() => ConnectionReset?.Invoke(ClientInfo));

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

        private async Task<object> InvokeMethodAsync(ISocketPacket packet)
        {
            Type t = Parent.GetType();
            var method = t.GetMethod(packet.MethodName);

            if (method == null)
            {
                throw new Exception("not found Method");
            }
            object ret = (object)await Task.Run(() => method.Invoke(Parent, new object[] { packet.PackData }));

            return ret;
        }

    }
}
