using Sockety.Client;
using Sockety.Model;
using MessagePack;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Sockety.Server
{
    public class ServerCore<T> : IDisposable where T :IService
    {
        #region IDisposable
        public void Dispose()
        {
            SocketClient<T>.GetInstance().ClientHubs.ForEach(x => x.Dispose());
            if (MainListener != null && MainListener.Connected == true)
            {
                MainListener.Disconnect(false);
                MainListener = null;
            }
        }
        #endregion

        private Socket MainListener;
        private T Parent;
        CancellationTokenSource stoppingCts;
        /// <summary>
        /// クライアント切断時に発火
        /// </summary>
        public Action<ClientInfo> ConnectionReset;

        public void Start(IPEndPoint localEndPoint, CancellationTokenSource _stoppingCts,T parent)
        {
            Parent = parent;
            stoppingCts = _stoppingCts;

            // メイン接続のTCP/IPを作成
            MainListener = new Socket(localEndPoint.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            MainListener.Bind(localEndPoint);
            MainListener.Listen(10);


            Task.Run(async () => { 
                //クライアント接続スレッド
                while (!stoppingCts.IsCancellationRequested)
                {
                    try
                    {
                        Console.WriteLine("Waiting for a connection...");
                        Socket handler = await MainListener.AcceptAsync();
                        //クライアント情報を受信
                        var clientInfo = ClientInfoReceive(handler);
                        if (ClientInfoManagement(clientInfo) == true)
                        {
                            Console.WriteLine($"ClientInfo ClientID:{clientInfo.ClientID} Name:{clientInfo.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"ReConnect ClientInfo ClientID:{clientInfo.ClientID} Name:{clientInfo.Name}");
                        }

                        //Udp接続
                        var CanUDPConnectPort = UserCommunicateService<T>.Get().Where(x => x.IsConnect == false).First();

                        //Udpポート番号を送信
                        handler.Send(MessagePackSerializer.Serialize(CanUDPConnectPort.UdpPortNumber));

                        //Udp HolePunching
                        var ret = UdpConnect(CanUDPConnectPort.UdpPortNumber);
                        CanUDPConnectPort.PunchingSocket = ret.s;
                        CanUDPConnectPort.PunchingPoint = ret.p;
                        CanUDPConnectPort.IsConnect = true;

                        // クライアントが接続したので、受付スレッドを開始する
                        var clientHub = new ClientHub<T>(_handler: handler,
                            _clientInfo: clientInfo,
                            udpPort: CanUDPConnectPort,
                            _stoppingCts: stoppingCts,
                            userClass: Parent,
                            parent: this);


                        clientHub.ConnectionReset = ConnectionReset;
                        clientHub.Run();

                        SocketClient<T>.GetInstance().ClientHubs.Add(clientHub);
                    }catch(Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            });
        }

        /// <summary>
        /// UDP HolePunchingでUDP接続する
        /// </summary>
        /// <param name="PortNumber"></param>
        /// <returns></returns>
        internal (Socket s , IPEndPoint p) UdpConnect(int PortNumber)
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
                Console.WriteLine($"BindAddress : {BindAddress.ToString()}");
                PunchingSocket.Bind(new IPEndPoint(BindAddress, PortNumber));
            }
            catch
            {
                try
                {
                    BindAddress = NetworkInterface.IPAddresses[0];
                    Console.WriteLine($"BindAddress : {BindAddress.ToString()}");
                    PunchingSocket.Bind(new IPEndPoint(BindAddress, PortNumber));

                }
                catch(Exception ex)
                {
                    throw ex;
                }
            } 
            
            var PunchingPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            return (PunchingSocket, PunchingPoint);
        }

        private bool ClientInfoManagement(ClientInfo clientInfo)
        {
            var clients = SocketClient<T>.GetInstance().ClientHubs;

            if (clients.Any(x => x.ClientInfo.ClientID == clientInfo.ClientID) == true)
            {
                //すでにClientIDがあるので再接続
                return false;
            }
            return true;
        }

        /// <summary>
        /// UDPにてデータを送信する
        /// </summary>
        /// <param name="data"></param>
        internal void BroadCastUDPNoReturn(SocketyPacketUDP packet)
        {
            SocketClient<T>.GetInstance().ClientHubs.ForEach(x =>
            {
                x.SendUdp(packet);
            });

        }

        /// <summary>
        /// TCPにてデータを送信する
        /// </summary>
        /// <param name="ClientMethodName"></param>
        /// <param name="data"></param>
        public void BroadCastNoReturn(string ClientMethodName,object data, List<Group> GroupLists = null)
        {
            List<ClientHub<T>> DisConnction = new List<ClientHub<T>>();

            List<ClientHub<T>> SendLists;
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
                try
                {
                    x.SendNonReturn(ClientMethodName, data);
                }
                catch (SocketException)
                {
                    //切断が発覚したので、切断リストに追加
                    DisConnction.Add(x);
                }
            });

            if (DisConnction.Count > 0)
            {
                //切断処理を行う
                DisConnction.ForEach(x => { 
                    SocketClient<T>.GetInstance().ClientHubs.Remove(x);
                    x.KillSW = true;
                    Console.WriteLine("BroadCastNoReturn DisConnect");

                    //通信切断
                    Task.Run(() => ConnectionReset?.Invoke(x.ClientInfo));
                });
            }

        }

        private ClientInfo ClientInfoReceive(Socket handler)
        {
            byte[] bytes = new Byte[SocketySetting.MAX_BUFFER];
            handler.Receive(bytes);
            ClientInfo clientInfo = MessagePackSerializer.Deserialize<ClientInfo>(bytes);

            return clientInfo;
        }
    }
}
