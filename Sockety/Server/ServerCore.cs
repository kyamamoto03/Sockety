using MessagePack;
using Microsoft.Extensions.Logging;
using Sockety.Filter;
using Sockety.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sockety.Server
{
    public class ServerCore<T> : IDisposable where T : IService
    {
        #region IDisposable
        public void Dispose()
        {
            SocketClient<T>.GetInstance().ClientHubs.ForEach(x => x.Dispose());
            if (MainListener != null )
            {
                MainListener.Stop();
                MainListener = null;
            }
        }
        #endregion
        private ILogger Logger;

        private TcpListener MainListener;
        private T Parent;
        CancellationTokenSource stoppingCts;

        public SocketyFilters SocketyFilters { get; private set; } = new SocketyFilters();
        /// <summary>
        /// クライアント切断時に発火
        /// </summary>
        public Action<ClientInfo> ConnectionReset;

        public ServerCore(ILogger logger)
        {
            Logger = logger;
        }

        private ServerSetting serverSetting;
        public void Start(IPEndPoint localEndPoint, CancellationTokenSource _stoppingCts, T parent, ServerSetting _serverSetting = null)
        {
            Parent = parent;
            stoppingCts = _stoppingCts;
            serverSetting = _serverSetting;

            if (serverSetting == null)
            {
                serverSetting = new ServerSetting { UseSSL = false };
            }
            // メイン接続のTCP/IPを作成
            MainListener = new TcpListener(localEndPoint.Address, localEndPoint.Port);
            MainListener.Start();


            Task.Run(async () =>
            {
                //クライアント接続スレッド
                while (!stoppingCts.IsCancellationRequested)
                {
                    try
                    {
                        Logger.LogInformation("Waiting for a connection...");
                        TcpClient handler = await MainListener.AcceptTcpClientAsync();

                        //接続方法をクライアントに伝える
                        SendConnectSetting(handler.GetStream(), serverSetting);

                        Stream CommunicateStream;
                        if (serverSetting.UseSSL == true){
                            Logger.LogInformation("UseSSL");
                            //SSL設定
                            SslStream sslStream = new SslStream(handler.GetStream());
                            sslStream.AuthenticateAsServer(serverSetting.Certificate, false, System.Security.Authentication.SslProtocols.Tls12, true);
                            CommunicateStream = sslStream as Stream;
                        }
                        else
                        {
                            Logger.LogInformation("NonSSL");
                            //平文
                            CommunicateStream = handler.GetStream();
                        }

                        //クライアント情報を受信
                        var clientInfo = ClientInfoReceive(CommunicateStream);
                        if (ClientInfoManagement(clientInfo) == true)
                        {
                            Logger.LogInformation($"ClientInfo ClientID:{clientInfo.ClientID} Name:{clientInfo.Name}");
                        }
                        else
                        {
                            Logger.LogInformation($"ReConnect ClientInfo ClientID:{clientInfo.ClientID} Name:{clientInfo.Name}");
                        }

                        //Udp接続
                        var CanUDPConnectPort = UserCommunicateService<T>.Get().Where(x => x.IsConnect == false).First();

                        //Udpポート番号を送信
                        var portData = MessagePackSerializer.Serialize(CanUDPConnectPort.UdpPortNumber);
                        CommunicateStream.Write(portData, 0, portData.Length);

                        //Udp HolePunching
                        var ret = UdpConnect(CanUDPConnectPort.UdpPortNumber);
                        CanUDPConnectPort.PunchingSocket = ret.s;
                        CanUDPConnectPort.PunchingPoint = ret.p;
                        CanUDPConnectPort.IsConnect = true;

                        // クライアントが接続したので、受付スレッドを開始する
                        var clientHub = new ClientHub<T>(_handler: handler,
                            _stream: CommunicateStream,
                            _clientInfo: clientInfo,
                            udpPort: CanUDPConnectPort,
                            userClass: Parent,
                            logger: Logger,
                            _filters: SocketyFilters);


                        clientHub.ConnectionReset = ConnectionReset;
                        clientHub.Run();

                        SocketClient<T>.GetInstance().AddClientHub(clientHub);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            });
        }

        /// <summary>
        /// ServerSettingを平文で送信
        /// </summary>
        /// <param name="networkStream"></param>
        /// <param name="serverSetting"></param>
        private void SendConnectSetting(NetworkStream networkStream, ServerSetting serverSetting)
        {
            var setting = new ConnectionSetting();
            if (serverSetting.UseSSL == true)
            {
                setting.UseSSL = true;
            }
            else
            {
                setting.UseSSL = false;
            }
            var data = MessagePackSerializer.Serialize(setting);
            var sizeb = BitConverter.GetBytes(data.Length);
            networkStream.Write(sizeb, 0, sizeof(int));
            networkStream.Write(data, 0, data.Length);

        }

        /// <summary>
        /// UDP HolePunchingでUDP接続する
        /// </summary>
        /// <param name="PortNumber"></param>
        /// <returns></returns>
        internal (Socket s, IPEndPoint p) UdpConnect(int PortNumber)
        {
            var WaitingServerAddress = IPAddress.Any;
            IPEndPoint groupEP = new IPEndPoint(WaitingServerAddress, PortNumber);

            //クライアントが設定してくるIPあどれっす
            string TargetAddress;
            //クライアントからのメッセージ(UDPホールパンチング）を待つ
            //groupEPにNATが変換したアドレス＋ポート番号は入ってくる
            using (var udpClient = new UdpClient(PortNumber))
            {
                //Udp Hole Puchingをするために何かしらのデータを受信する(ここではクライアントが指定したサーバのアドレス)
                TargetAddress = Encoding.UTF8.GetString(udpClient.Receive(ref groupEP));
            }

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

        private bool ClientInfoManagement(ClientInfo clientInfo)
        {
            lock (SocketClient<T>.GetInstance().ClientHubs)
            {
                var clients = SocketClient<T>.GetInstance().ClientHubs;

                if (clients.Any(x => x.ClientInfo.ClientID == clientInfo.ClientID) == true)
                {
                    //すでにClientIDがあるので再接続
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// UDPにてデータを送信する
        /// </summary>
        /// <param name="data"></param>
        public void BroadCastUDPNoReturn(ClientInfo clientInfo, byte[] data, List<Group> GroupLists = null)
        {
            if (data != null && data.Length > SocketySetting.MAX_UDP_SIZE)
            {
                throw new SocketyException(SocketyException.SOCKETY_EXCEPTION_ERROR.BUFFER_OVER);
            }

            //パケット分割
            //var packets = PacketSerivce<T>.PacketSplit(clientInfo, data);
            var packet = new SocketyPacketUDP { MethodName = "Udp", clientInfo = clientInfo, PacketID = Guid.NewGuid(), PacketNo = 1,PackData = data };

            List<ClientHub<T>> SendLists;
            lock (SocketClient<T>.GetInstance().ClientHubs)
            {
                if (GroupLists == null)
                {
                    SendLists = SocketClient<T>.GetInstance().ClientHubs;
                }
                else
                {
                    SendLists = new List<ClientHub<T>>();
                    ///送信先をグループで検索しリストを作成
                    SocketClient<T>.GetInstance().ClientHubs.ForEach(x =>
                    {
                        foreach (var g in GroupLists)
                        {
                            if (x.ClientInfo.JoinGroups.Contains(g) == true)
                            {
                                if (SendLists.Contains(x) == false)
                                {
                                    SendLists.Add(x);
                                    break;
                                }
                            }
                        }
                    });

                }

                SendLists.ForEach(x =>
                {
                    Task.Run(() =>
                    {
                        x.SendUdp(packet);
                    });
                });
            }
        }

        /// <summary>
        /// TCPにてデータを送信する
        /// </summary>
        /// <param name="ClientMethodName"></param>
        /// <param name="data"></param>
        public void BroadCastNoReturn(string ClientMethodName, byte[] data, List<Group> GroupLists = null)
        {
            if (data != null && data.Length > SocketySetting.MAX_BUFFER)
            {
                throw new SocketyException(SocketyException.SOCKETY_EXCEPTION_ERROR.BUFFER_OVER);
            }
            List<ClientHub<T>> DisConnction = new List<ClientHub<T>>();

            List<ClientHub<T>> SendLists;
            if (GroupLists == null)
            {
                SendLists = SocketClient<T>.GetInstance().ClientHubs;
            }
            else
            {
                SendLists = new List<ClientHub<T>>();
                lock (SocketClient<T>.GetInstance().ClientHubs)
                {
                    ///送信先をグループで検索しリストを作成
                    SocketClient<T>.GetInstance().ClientHubs.ForEach(x =>
                    {
                        foreach (var g in GroupLists)
                        {
                            if (x.ClientInfo.JoinGroups.Contains(g) == true)
                            {
                                if (SendLists.Contains(x) == false)
                                {
                                    SendLists.Add(x);
                                    break;
                                }
                            }
                        }
                    });
                }

            }

            SendLists.ForEach(async (x) =>
            {
                try
                {
                    await x.SendNonReturn(ClientMethodName, data);
                }
                catch (IOException)
                {
                    //切断が発覚したので、切断リストに追加
                    DisConnction.Add(x);
                }
            });

            if (DisConnction.Count > 0)
            {
                //切断処理を行う
                DisConnction.ForEach(x =>
                {
                    SocketClient<T>.GetInstance().RemoveClientHub(x);
                    x.ThreadCancel();
                    Logger.LogInformation($"BroadCastNoReturn DisConnect:{x.ClientInfo.ClientID}");

                    //通信切断
                    Task.Run(() => ConnectionReset?.Invoke(x.ClientInfo));
                });
            }

        }

        /// <summary>
        /// 指定したクライアントに送信する
        /// </summary>
        /// <param name="SendClient"></param>
        /// <param name="ClientMethodName"></param>
        /// <param name="data"></param>
        public void SendNoReturn(ClientInfo SendClient,string ClientMethodName, byte[] data)
        {
            if (data != null && data.Length > SocketySetting.MAX_BUFFER)
            {
                throw new SocketyException(SocketyException.SOCKETY_EXCEPTION_ERROR.BUFFER_OVER);
            }

            var SendClientHub = SocketClient<T>.GetInstance().ClientHubs.FirstOrDefault(x => x.ClientInfo.Equals(SendClient));
            if (SendClientHub != null)
            {
                try
                {
                    SendClientHub.SendNonReturn(ClientMethodName, data);
                }
                catch (IOException)
                {
                    SocketClient<T>.GetInstance().RemoveClientHub(SendClientHub);
                    SendClientHub.ThreadCancel();
                    Logger.LogInformation($"SendNoReturn DisConnect:{SendClientHub.ClientInfo.ClientID}");

                    //通信切断
                    Task.Run(() => ConnectionReset?.Invoke(SendClientHub.ClientInfo));
                }
            }
            else
            {
                Logger.LogError("NO_CLIENT");
                throw new SocketyException(SocketyException.SOCKETY_EXCEPTION_ERROR.NO_CLIENT);
            }
        }

        private ClientInfo ClientInfoReceive(Stream ns)
        {
            byte[] bytes = new Byte[SocketySetting.MAX_BUFFER];
            ns.Read(bytes, 0, bytes.Length);
            ClientInfo clientInfo = MessagePackSerializer.Deserialize<ClientInfo>(bytes);

            return clientInfo;
        }
    }
}
