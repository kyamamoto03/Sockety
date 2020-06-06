using MessagePack;
using Microsoft.Extensions.Logging;
using Sockety.Model;
using Sockety.Service;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Sockety.Server
{
    public class ClientHub<T> : IDisposable where T : IService
    {
        private Socket serverSocket = null;
        private Thread TcpThread;
        public ClientInfo ClientInfo { get; private set; }
        private T UserClass;
        private UdpPort<T> UdpPort;
        private ServerCore<T> Parent;
        /// <summary>
        /// クライアントが切断時に発火
        /// </summary>
        public Action<ClientInfo> ConnectionReset;
        public bool KillSW = false;
        private readonly ILogger Logger;

        PacketSerivce<T> PacketSerivce;

        public ClientHub(Socket _handler,
            ClientInfo _clientInfo,
            UdpPort<T> udpPort,
            T userClass,
            ServerCore<T> parent,
            ILogger logger)
        {
            this.UserClass = userClass;
            this.serverSocket = _handler;
            this.ClientInfo = _clientInfo;
            this.UdpPort = udpPort;
            this.Parent = parent;
            this.Logger = logger;

            PacketSerivce = new PacketSerivce<T>();
            PacketSerivce.SetUp(userClass);
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


        internal void SendNonReturn(string ClientMethodName, byte[] data)
        {
            try
            {
                var packet = new SocketyPacket() { MethodName = ClientMethodName, PackData = data };
                var d = MessagePackSerializer.Serialize(packet);
                serverSocket.Send(d);
            }
            catch (SocketException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        internal void SendUdp(SocketyPacketUDP packet)
        {
            try
            {
                var bytes = MessagePackSerializer.Serialize(packet);
                UdpPort.PunchingSocket.SendTo(bytes, SocketFlags.None, UdpPort.PunchingPoint);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public class StateObject
        {
            // Client socket.  
            public Socket workSocket = null;
            // Receive buffer.  
            public byte[] Buffer;
        }

        public void Run()
        {
            TcpThread = new Thread(new ThreadStart(ReceiveProcess));
            TcpThread.Start();

            //UDPの受信を開始
            var UdpStateObject = new StateObject() { 
                Buffer = new byte[SocketySetting.MAX_BUFFER], 
                workSocket = UdpPort.PunchingSocket };

            UdpPort.PunchingSocket.BeginReceive(UdpStateObject.Buffer, 0, UdpStateObject.Buffer.Length, 0, new AsyncCallback(UdpReceiver), UdpStateObject);
        }

        /// <summary>
        /// UDP受信
        /// </summary>
        /// <param name="ar"></param>
        private void UdpReceiver(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;
                int bytesRead = client.EndReceive(ar);
                if (bytesRead > 0)
                {
                    Task.Run(() =>
                    {
                        var packet = MessagePackSerializer.Deserialize<SocketyPacketUDP>(state.Buffer);

                        //親クラスを呼び出す
                        PacketSerivce.ReceiverSocketyPacketUDP(packet);
                    });

                    //  受信を再スタート  
                    client.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0,
                        new AsyncCallback(UdpReceiver), state);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// 受信を一括して行う
        /// </summary>
        private async void ReceiveProcess()
        {
            byte[] CommunicateButter = new byte[SocketySetting.MAX_BUFFER];
            while (!KillSW)
            {
                try
                {
                    if (serverSocket == null)
                    {
                        return;
                    }
                    int bytesRec = serverSocket.Receive(CommunicateButter);
                    var packet = MessagePackSerializer.Deserialize<SocketyPacket>(CommunicateButter);

                    //メソッドの戻り値を詰め替える
                    packet.PackData = await InvokeMethodAsync(packet);
                    //送り返す
                    serverSocket.Send(MessagePackSerializer.Serialize(packet));
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        Logger.LogInformation($"ReceiveProcess DisConnect:{ClientInfo.ClientID}");

                        //クライアント一覧から削除
                        SocketClient<T>.GetInstance().ClientHubs.Remove(this);
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

        private async Task<byte[]> InvokeMethodAsync(SocketyPacket packet)
        {
            Type t = UserClass.GetType();
            var method = t.GetMethod(packet.MethodName);

            if (method == null)
            {
                throw new Exception("not found Method");
            }
            byte[] ret = (byte[])await Task.Run(() => method.Invoke(UserClass, new object[] { ClientInfo, packet.PackData }));

            return ret;
        }

    }
}
