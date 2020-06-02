using Sockety.Client;
using Sockety.Model;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Sockety.Service;

namespace Sockety.Server
{
    public class ClientHub<T> : IDisposable where T : IService
    {
        private Socket serverSocket = null;
        private Thread TcpThread;
        private Thread UdpThread;
        public ClientInfo ClientInfo { get; private set; }
        private T UserClass;
        private CancellationTokenSource stoppingCts;
        private UdpPort<T> UdpPort;
        private ServerCore<T> Parent;
        /// <summary>
        /// クライアントが切断時に発火
        /// </summary>
        public Action<ClientInfo> ConnectionReset;
        public bool KillSW = false;

        PacketSerivce<T> PacketSerivce;

        public ClientHub(Socket _handler, 
            ClientInfo _clientInfo, 
            UdpPort<T> udpPort, 
            CancellationTokenSource _stoppingCts,
            T userClass,
            ServerCore<T>parent)
        {
            this.UserClass = userClass;
            this.serverSocket = _handler;
            this.ClientInfo = _clientInfo;
            this.UdpPort = udpPort;
            this.stoppingCts = _stoppingCts;
            this.Parent = parent;

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
            }catch(SocketException ex)
            {
                throw ex;
            }catch(Exception ex)
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

        public void Run()
        {
            TcpThread = new Thread(new ThreadStart(ReceiveProcess));
            TcpThread.Start();

            UdpThread = new Thread(new ThreadStart(UdpReceiveProcess));
            UdpThread.Start();
        }

        private void UdpReceiveProcess()
        {
            byte[] CommunicateButter = new byte[SocketySetting.MAX_BUFFER];
            UdpPort.PunchingSocket.ReceiveTimeout = 5000;
            while (!KillSW)
            {
                try
                {
                    int cnt = UdpPort.PunchingSocket.Receive(CommunicateButter);
                    Task.Run(() => { 
                        var packet = MessagePackSerializer.Deserialize<SocketyPacketUDP>(CommunicateButter);

                        //親クラスを呼び出す
                        PacketSerivce.ReceiverSocketyPacketUDP(packet);
                    });
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
            byte[] ret = (byte[])await Task.Run(() => method.Invoke(UserClass, new object[] {ClientInfo, packet.PackData }));

            return ret;
        }

    }
}
