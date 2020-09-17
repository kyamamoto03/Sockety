using MessagePack;
using Microsoft.Extensions.Logging;
using Sockety.Attribute;
using Sockety.Filter;
using Sockety.Model;
using Sockety.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sockety.Server
{
    public class ClientHub<T> : IDisposable where T : IService
    {
        private TcpClient serverSocket = null;
        private Stream commnicateStream;
        private SocketyCryptService CryptService;

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

        internal ClientHub(TcpClient _handler,
            Stream _stream,
            ClientInfo _clientInfo,
            T userClass,
            ILogger logger,
            SocketyFilters _filters,
            SocketyCryptService CryptService)
        {
            this.UserClass = userClass;
            this.serverSocket = _handler;
            this.ClientInfo = _clientInfo;
            this.Logger = logger;
            this.commnicateStream = _stream;
            this.SocketyFilters = _filters;
            this.CryptService = CryptService;

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
                //throw ex;
                Logger.LogInformation($"{ex}");
            }
        }
        public List<HeartBeat> ReceiveHeartBeats = new List<HeartBeat>();
        /// <summary>
        /// HeartBeat受信処理
        /// </summary>
        private void ReceiveHeartBeat(SocketyPacket packet)
        {
            if (packet.SocketyPacketType == SocketyPacket.SOCKETY_PAKCET_TYPE.FinishHeartBeat)
            {
                Task.Run(() =>
                {
                    //正常な切断処理を行う
                    NormalDisConnect();
                });
            }
            else
            {
                lock (ReceiveHeartBeats)
                {
                    ReceiveHeartBeats.Add(new HeartBeat { ReceiveDate = DateTime.Now });
                }
            }
        }

        private void SurveillanceHeartBeat()
        {
            lock (ReceiveHeartBeats)
            {
                ReceiveHeartBeats.Clear();
            }
            Task.Run(() =>
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
                                Logger.LogInformation("SurveillanceHeartBeat Done.");
                                //監視終了
                                break;
                            }
                        }
                    }
                    Thread.Sleep(5000);
                }
                DisConnect();
            });
        }

        #endregion

        object lockObject = new object();
        internal void SendNonReturn(string ClientMethodName, byte[] data)
        {
            try
            {
                lock (lockObject)
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
                    var preData = MessagePackSerializer.Serialize(packet);
                    if (CryptService != null)
                    {
                        preData = CryptService.Encrypt(preData);
                    }

                    UdpPort.PunchingSocket.SendTo(preData, SocketFlags.None, UdpPort.PunchingPoint);
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
            InitUDP();

            MakeHeartBeat();

            SurveillanceHeartBeat();

            var TcpReceiveThread = new Thread(new ThreadStart(ReceiveProcess));
            TcpReceiveThread.Name = "ReceiveProcess";
            TcpReceiveThread.Start();

        }
        private void InitUDP()
        {
            //Udp接続
            var CanUDPConnectPort = UserCommunicateService<T>.Get().Where(x => x.IsConnect == false).First();

            //Udpポート番号を送信
            var portData = MessagePackSerializer.Serialize(CanUDPConnectPort.UdpPortNumber);
            commnicateStream.Write(portData, 0, portData.Length);

            //Udp HolePunching
            var ret = UdpConnect(commnicateStream, CanUDPConnectPort.UdpPortNumber, CryptService);
            CanUDPConnectPort.PunchingSocket = ret.s;
            CanUDPConnectPort.PunchingPoint = ret.p;
            CanUDPConnectPort.IsConnect = true;

            this.UdpPort = CanUDPConnectPort;
            //UDPの受信を開始
            var UdpStateObject = new StateObject()
            {
                Buffer = new byte[SocketySetting.MAX_UDP_SIZE],
                workSocket = UdpPort.PunchingSocket
            };

            UdpPort.PunchingSocket.BeginReceive(UdpStateObject.Buffer, 0, UdpStateObject.Buffer.Length, 0, UdpReceiver, UdpStateObject);

        }

        /// <summary>
        /// UDP HolePunchingでUDP接続する
        /// </summary>
        /// <param name="PortNumber"></param>
        /// <returns></returns>
        private (Socket s, IPEndPoint p) UdpConnect(Stream stream, int PortNumber, SocketyCryptService cryptService)
        {
            var WaitingServerAddress = IPAddress.Any;
            IPEndPoint groupEP = new IPEndPoint(WaitingServerAddress, PortNumber);

            //クライアントが設定してくるIPあどれっす
            string TargetAddress;

            SocketyPacketUDP controlPacket;
            //クライアントからのメッセージ(UDPホールパンチング）を待つ
            //groupEPにNATが変換したアドレス＋ポート番号は入ってくる
            using (var udpClient = new UdpClient(PortNumber))
            {
                //Udp Hole Puchingをするために何かしらのデータを受信する(ここではクライアントが指定したサーバのアドレス)
                //TargetAddress = Encoding.UTF8.GetString(udpClient.Receive(ref groupEP));
                var data = udpClient.Receive(ref groupEP);
                if (cryptService != null)
                {
                    data = cryptService.Decrypt(data);
                }
                controlPacket = MessagePackSerializer.Deserialize<SocketyPacketUDP>(data);
            }
            TargetAddress = Encoding.UTF8.GetString(controlPacket.PackData);

            byte[] OK = new byte[1] { 0x01 };

            stream.Write(OK, 0, OK.Length);

            //NATで変換されたIPアドレスおよびポート番号
            var ip = groupEP.Address.ToString();
            var port = groupEP.Port;

            var PunchingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //ソースアドレスを設定する(NATが変換できるように、クライアントが指定した宛先を設定)
            IPAddress BindAddress;
            try
            {
                BindAddress = IPAddress.Parse(TargetAddress);
                Logger.LogInformation($"BindAddress : {BindAddress.ToString()}");
                PunchingSocket.Bind(new IPEndPoint(BindAddress, PortNumber));
            }
            catch
            {
                try
                {
                    BindAddress = NetworkInterface.IPAddresses[0];
                    Logger.LogInformation($"BindAddress : {BindAddress.ToString()}");
                    PunchingSocket.Bind(new IPEndPoint(BindAddress, PortNumber));

                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            var PunchingPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            return (PunchingSocket, PunchingPoint);
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
                    byte[] ReceiveData = new byte[bytesRead];
                    Array.Copy(state.Buffer, ReceiveData, bytesRead);
                    if (CryptService != null)
                    {
                        ReceiveData = CryptService.Decrypt(ReceiveData);
                    }
                    var packet = MessagePackSerializer.Deserialize<SocketyPacketUDP>(ReceiveData);
                    if (packet.PacketType == SocketyPacketUDP.PACKET_TYPE.DATA)
                    {
                        //親クラスを呼び出す
                        UserClass.UdpReceive(packet.clientInfo, packet.PackData);

                    }
                    //  受信を再スタート  
                    client.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0,
                        UdpReceiver, state);
                }
            }
            catch (SocketException ex)
            {

            }
            catch (Exception e)
            {
                //Logger.LogInformation(e.ToString());
            }
        }

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
                    lock (lockObject)
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
                                if (packet.SocketyPacketType != SocketyPacket.SOCKETY_PAKCET_TYPE.Data)
                                {
                                    ReceiveHeartBeat(packet);
                                }
                                else
                                {
                                    //メソッドの戻り値を詰め替える
                                    packet.PackData = InvokeMethod(method, packet);

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
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
                Thread.Sleep(10);
            }
            TcpReceiveThreadFinishEvent.Set();
        }

        /// <summary>
        /// クライアント起因による切断処理
        /// </summary>
        /// <returns></returns>
        private void DisConnect()
        {
            if (serverSocket != null)
            {
                Logger.LogInformation($"DisConnect:{ClientInfo.ClientID}");
                NormalDisConnect();
                //通信切断
                Task.Run(() => ConnectionReset?.Invoke(ClientInfo));
            }
        }
        private void NormalDisConnect()
        {
            Logger.LogInformation($"NormalDisConnect:{ClientInfo.ClientID}");

            //クライアント一覧から削除
            SocketClient<T>.GetInstance().ClientHubs.Remove(this);
            serverSocket = null;

            ThreadCancel();
            commnicateStream.Close();
            //Udpの切断処理
            UdpPort.PunchingSocket.Close();
            TcpReceiveThreadFinishEvent.WaitOne();
            UdpReceiveThreadFinishEvent.WaitOne();
            //Udpのポートが使えるようにする
            UdpPort.IsConnect = false;
        }

        private MethodInfo GetMethod(SocketyPacket packet)
        {
            Type t = UserClass.GetType();
            if (packet.SocketyPacketType == SocketyPacket.SOCKETY_PAKCET_TYPE.HaertBeat ||
                packet.SocketyPacketType == SocketyPacket.SOCKETY_PAKCET_TYPE.FinishHeartBeat)
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
