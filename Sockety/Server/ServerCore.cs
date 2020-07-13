using MessagePack;
using Microsoft.Extensions.Logging;
using Sockety.Filter;
using Sockety.Model;
using Sockety.Service;
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

        private static string AES_IV = @"pf69DLcGrWFyZcMK";
        private static string AES_Key = @"9Fix4L4bdGPKeKWY";

        private TcpListener MainListener;
        private T Parent;
        CancellationTokenSource stoppingCts;

        public readonly int DefaultStartUDP = 11000;
        public readonly int DefaultEndUDP = 11120;

        public SocketyFilters SocketyFilters { get; private set; } = new SocketyFilters();
        /// <summary>
        /// クライアント切断時に発火
        /// </summary>
        public Action<ClientInfo> ConnectionReset;

        public ServerCore(ILogger logger)
        {
            Logger = logger;
            InitUDP(DefaultStartUDP, DefaultEndUDP);
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
                        SocketyCryptService cryptService = null;
                        if (serverSetting.UseSSL == true){
                            Logger.LogInformation("UseSSL");
                            //SSL設定
                            SslStream sslStream = new SslStream(handler.GetStream());
                            sslStream.AuthenticateAsServer(serverSetting.Certificate, false, System.Security.Authentication.SslProtocols.Tls12, true);
                            CommunicateStream = sslStream as Stream;
                            cryptService = new SocketyCryptService(AES_IV, AES_Key);
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


                        // クライアントが接続したので、受付スレッドを開始する
                        var clientHub = new ClientHub<T>(_handler: handler,
                            _stream: CommunicateStream,
                            _clientInfo: clientInfo,
                            userClass: Parent,
                            logger: Logger,
                            _filters: SocketyFilters,
                            CryptService: cryptService);


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
        /// UDPポート情報の初期化
        /// </summary>
        /// <param name="StartPort"></param>
        /// <param name="EndPort"></param>
        public void InitUDP(int StartPort,int EndPort)
        {
            UserCommunicateService<T>.Init(StartPort: StartPort, EndPort: EndPort);

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

            var packet = new SocketyPacketUDP { MethodName = "Udp", clientInfo = clientInfo, PacketID = Guid.NewGuid(), PacketNo = 1,PackData = data,PacketType = SocketyPacketUDP.PACKET_TYPE.DATA };

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
                    x.SendUdp(packet);
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
            lock (SocketClient<T>.GetInstance().ClientHubs)
            {
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
                        x.SendNonReturn(ClientMethodName, data);
                    }
                    catch (IOException)
                    {
                    }
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
