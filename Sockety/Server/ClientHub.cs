using MessagePack;
using Microsoft.Extensions.Logging;
using Sockety.Attribute;
using Sockety.Base;
using Sockety.Filter;
using Sockety.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Sockety.Server
{
    public class ClientHub<T> : IDisposable where T : IService
    {
        private TcpClient serverSocket = null;
        private Stream commnicateStream;

        public ClientInfo ClientInfo { get; private set; }
        private T UserClass;
        private UdpPort<T> UdpPort;
        /// <summary>
        /// クライアントが切断時に発火
        /// </summary>
        public Action<ClientInfo> ConnectionReset;
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private readonly ILogger Logger;
        private SocketyFilters SocketyFilters;

        /// <summary>
        /// TCP受信スレッド終了イベント
        /// </summary>
        private ManualResetEvent TcpReceiveThreadFinishEvent = new ManualResetEvent(false);
        /// <summary>
        /// UDP受信スレッド終了イベント
        /// </summary>
        private ManualResetEvent UdpReceiveThreadFinishEvent = new ManualResetEvent(false);

        public void ThreadCancel()
        {
            _stoppingCts.Cancel();
        }

        public ClientHub(TcpClient _handler,
            Stream _stream,
            ClientInfo _clientInfo,
            UdpPort<T> udpPort,
            T userClass,
            ILogger logger,
            SocketyFilters _filters)
        {
            this.UserClass = userClass;
            this.serverSocket = _handler;
            this.ClientInfo = _clientInfo;
            this.UdpPort = udpPort;
            this.Logger = logger;
            this.commnicateStream = _stream;
            this.SocketyFilters = _filters;

            MakeHeartBeat();

#pragma warning disable CS4014 // この呼び出しは待機されなかったため、現在のメソッドの実行は呼び出しの完了を待たずに続行されます
            SurveillanceHeartBeat();
#pragma warning restore CS4014 // この呼び出しは待機されなかったため、現在のメソッドの実行は呼び出しの完了を待たずに続行されます
        }

        public void Dispose()
        {
            if (serverSocket != null)
            {
                commnicateStream.Close();
                serverSocket.Close();
                serverSocket = null;
            }
        }

        #region HeartBeat
        private void MakeHeartBeat()
        {
            var thread = new Thread(new ThreadStart(SendHeartBeatProcess));
            thread.Name = "SendHeartBeatProcess";
            thread.Start();

        }
        private void SendHeartBeatProcess()
        {
            while (!_stoppingCts.IsCancellationRequested)
            {
                SendHeartBeat();
                Thread.Sleep(1000);
            }
            Logger.LogInformation("MakeHeartBeat Done");
        }
        private void SendHeartBeat()
        {
            try
            {
                lock (lockObject)
                {
                    var packet = new SocketyPacket() { SocketyPacketType = SocketyPacket.SOCKETY_PAKCET_TYPE.HaertBeat };
                    var d = MessagePackSerializer.Serialize(packet);
                    PacketSize size = PacketSize.Create(d.Length);
                    var sizeb = size.GetBytes();
                    commnicateStream.Write(sizeb, 0, sizeb.Length);

                    commnicateStream.Write(d, 0, d.Length);
                }
            }
            catch (IOException ex)
            {
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public List<HeartBeat> ReceiveHeartBeats = new List<HeartBeat>();
        /// <summary>
        /// HeartBeat受信処理
        /// </summary>
        private void ReceiveHeartBeat()
        {
            lock (ReceiveHeartBeats)
            {
                ReceiveHeartBeats.Add(new HeartBeat { ReceiveDate = DateTime.Now });
            }
        }

        private async Task SurveillanceHeartBeat()
        {
            lock (ReceiveHeartBeats)
            {
                ReceiveHeartBeats.Clear();
            }
            await Task.Run(() =>
            {
                while (true)
                {
                    lock (ReceiveHeartBeats)
                    {
                        var LastHeartBeat = ReceiveHeartBeats.OrderByDescending(x => x.ReceiveDate).FirstOrDefault();
                        if (LastHeartBeat != null)
                        {
                            var diff = DateTime.Now - LastHeartBeat.ReceiveDate;
                            if (diff.TotalMilliseconds > SocketySetting.HEART_BEAT_LOST_TIME)
                            {
                                //監視終了
                                return;
                            }
                        }
                    }
                    Thread.Sleep(5000);
                }
            });
            DisConnect();
        }

        #endregion

        object lockObject = new object();
        internal void SendNonReturn(string ClientMethodName, byte[] data)
        {
            try
            {
                lock(lockObject)
                {
                    var packet = new SocketyPacket() { MethodName = ClientMethodName, PackData = data };
                    var d = MessagePackSerializer.Serialize(packet);
                    if (serverSocket != null)
                    {
                        PacketSize size = PacketSize.Create(d.Length);
                        var sizeb = size.GetBytes();
                        commnicateStream.Write(sizeb, 0, sizeb.Length);
                        commnicateStream.Write(d, 0, d.Length);
                    }
                }
            }
            catch (IOException ex)
            {
                //Logger.LogError($"SendNonReturn:{ex.ToString()}");
                return;
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
                lock (UdpPort.PunchingSocket)
                {
                    var bytes = MessagePackSerializer.Serialize(packet);
                    UdpPort.PunchingSocket.SendTo(bytes, SocketFlags.None, UdpPort.PunchingPoint);
                }
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
            var TcpReceiveThread = new Thread(new ThreadStart(ReceiveProcess));
            TcpReceiveThread.Name = "ReceiveProcess";
            TcpReceiveThread.Start();

            //UDPの受信を開始
            var UdpStateObject = new StateObject() { 
                Buffer = new byte[SocketySetting.MAX_UDP_SIZE], 
                workSocket = UdpPort.PunchingSocket };

            UdpPort.PunchingSocket.BeginReceive(UdpStateObject.Buffer, 0, UdpStateObject.Buffer.Length, 0, UdpReceiver, UdpStateObject);
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
                if (_stoppingCts.IsCancellationRequested)
                {
                    UdpReceiveThreadFinishEvent.Set();
                    return;
                }
                int bytesRead = client.EndReceive(ar);
                if (bytesRead > 0)
                {
                    var packet = MessagePackSerializer.Deserialize<SocketyPacketUDP>(state.Buffer);
                    //親クラスを呼び出す
                    UserClass.UdpReceive(packet.clientInfo, packet.PackData);

                    //  受信を再スタート  
                    client.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0,
                        UdpReceiver, state);
                }
            }
            catch (Exception e)
            {
                Logger.LogInformation(e.ToString());
            }
        }

        private AsyncLock TCPReceiveLock = new AsyncLock();
        /// <summary>
        /// 受信を一括して行う
        /// </summary>
        private void ReceiveProcess()
        {
            byte[] sizeb = new byte[sizeof(int)];

            Logger.LogInformation("ReceiveProcess Start");
            while (_stoppingCts.IsCancellationRequested == false)
            {
                try
                {
                    if (serverSocket == null)
                    {
                        TcpReceiveThreadFinishEvent.Set();
                        return;
                    }

                    int bytesRec = commnicateStream.Read(sizeb, 0, sizeof(int));
                    lock(lockObject)
                    {
                        if (bytesRec > 0)
                        {
                            int size = BitConverter.ToInt32(sizeb, 0);
                            byte[] buffer = new byte[size];
                            int DataSize = 0;
                            do
                            {

                                bytesRec = commnicateStream.Read(buffer, DataSize, size - DataSize);

                                DataSize += bytesRec;

                            } while (size > DataSize);

                            var packet = MessagePackSerializer.Deserialize<SocketyPacket>(buffer);

                            //AuthentificationFilter
                            bool AuthentificationSuccess = true;
                            var authentificationFilter = SocketyFilters.Get<IAuthenticationFIlter>();
                            var method = GetMethod(packet);

                            if (packet.SocketyPacketType == SocketyPacket.SOCKETY_PAKCET_TYPE.Data && authentificationFilter != null)
                            {
                                bool FindIgnore = false;

                                if (method.GetCustomAttribute<SocketyAuthentificationIgnoreAttribute>() != null)
                                {
                                    //SocketyAuthentificationIgnoreがあるメソッドは認証を行わない
                                    FindIgnore = true;
                                    AuthentificationSuccess = true;
                                }

                                if (FindIgnore == false)
                                {
                                    AuthentificationSuccess = authentificationFilter.Authentication(packet.Toekn);
                                }
                            }

                            if (AuthentificationSuccess == true)
                            {
                                if (packet.SocketyPacketType == SocketyPacket.SOCKETY_PAKCET_TYPE.HaertBeat)
                                {
                                    ReceiveHeartBeat();
                                }
                                else
                                {
                                    //メソッドの戻り値を詰め替える
                                    packet.PackData = InvokeMethod(method, packet);
                                    Logger.LogInformation(method.Name);

                                    //InvokeMethodAsyncの戻り値を送り返す
                                    var d = MessagePackSerializer.Serialize(packet);
                                    PacketSize packetSize = PacketSize.Create(d.Length);
                                    var sizeb2 = packetSize.GetBytes();
                                    commnicateStream.Write(sizeb2, 0, sizeb2.Length);
                                    commnicateStream.Write(d, 0, d.Length);
                                }
                            }
                            else
                            {
                                Logger.LogInformation($"Client Authentificateion Fail \r\n{packet.clientInfo.ToString()}");
                                //認証失敗は接続を切断
                                DisConnect();
                            }
                        }
                    }
                }
                catch (IOException ex)
                {
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// クライアント起因による切断処理
        /// </summary>
        /// <returns></returns>
        private void DisConnect()
        {
            Logger.LogInformation($"ReceiveProcess DisConnect:{ClientInfo.ClientID}");

            //クライアント一覧から削除
            SocketClient<T>.GetInstance().ClientHubs.Remove(this);
            serverSocket = null;
            //通信切断
            Task.Run(() => ConnectionReset?.Invoke(ClientInfo));

            ThreadCancel();
            TcpReceiveThreadFinishEvent.WaitOne();
            UdpReceiveThreadFinishEvent.WaitOne();
            //Udpのポートが使えるようにする
            UdpPort.IsConnect = false;
            //Udpの切断処理
            UdpPort.PunchingSocket.Close();
        }

        private MethodInfo GetMethod(SocketyPacket packet)
        {
            Type t = UserClass.GetType();
            if (packet.SocketyPacketType == SocketyPacket.SOCKETY_PAKCET_TYPE.HaertBeat)
            {
                return null;
            }

            var method = t.GetMethod(packet.MethodName);

            if (method == null)
            {
                throw new Exception("not found Method");
            }

            return method;
        }

        private byte[] InvokeMethod(MethodInfo method, SocketyPacket packet)
        {
            byte[] ret = (byte[])method.Invoke(UserClass, new object[] { ClientInfo, packet.PackData });

            return ret;
        }


    }
}
