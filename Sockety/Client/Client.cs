using MessagePack;
using Microsoft.Extensions.Logging;
using Sockety.Model;
using Sockety.Service;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml.Schema;

namespace Sockety.Client
{
    public class Client<T> : IDisposable where T : IService
    {
        private TcpClient serverSocket;
        private ClientReceiver<T> clientReceiver { get; } = new ClientReceiver<T>();
        private T Parent;
        private IPEndPoint ServerEndPoint;
        public ClientInfo clientInfo { get; private set; }
        public Action ConnectionReset;
        private string ServerHost;
        private ILogger Logger;
        private int PortNumber;
        private string UserName;

        public Client(ILogger logger)
        {
            Logger = logger;
        }
        /// <summary>
        /// 接続
        /// </summary>
        /// <param name="ServerHost"></param>
        /// <param name="PortNumber"></param>
        /// <param name="UserName"></param>
        /// <param name="parent"></param>
        public bool Connect(string ServerHost, int portNumber, string userName, T parent)
        {
            this.ServerHost = ServerHost;
            this.PortNumber = portNumber;
            this.UserName = userName;
            clientReceiver.ConnectionReset = ConnectionReset;
            Parent = parent;

            return ConnectProcess(CONNECT_TYPE.NEW_CONNECT);
        }

        /// <summary>
        /// 再接続処理
        /// </summary>
        /// <returns></returns>
        public bool ReConnect()
        {
            if (ServerEndPoint == null)
            {
                //一度も接続していない
                throw new Exception("Not Connect");
            }

            if (clientReceiver.Connected == false)
            {
                return ConnectProcess(CONNECT_TYPE.RE_CONNECT);
            }
            return false;
        }

        private bool ConnectProcess(CONNECT_TYPE ConnectType)
        {
            IPAddress host;
            if (IPAddress.TryParse(ServerHost, out host) == false)
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(ServerHost);
                host = ipHostInfo.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();
            }
            else
            {
                host = IPAddress.Parse(ServerHost);
            }
            ServerEndPoint = new IPEndPoint(host, PortNumber);

            try
            {
                //TCP接続
                serverSocket = new TcpClient(ServerEndPoint.Address.ToString(), ServerEndPoint.Port);

                Logger.LogInformation("Socket connected to {0}",
                    serverSocket.ToString());

                //新規の接続なのでClientInfoを作成
                clientInfo = CreateNewClientInfo(UserName);
                //接続出来たらクライアント情報を送る
                SendClientInfo(serverSocket, clientInfo);

                //Udp接続
                var UdpInfo = ConnectUdp(ServerHost, ReceiveUdpPort());

                //受信スレッド作成
                clientReceiver.Run(handler: serverSocket,
                    UdpSocket: UdpInfo.socket,
                    UdpEndPort: UdpInfo.point,
                    clientInfo: clientInfo,
                    Parent);

            }
            catch (ArgumentNullException ane)
            {
                Logger.LogError("ArgumentNullException : {0}", ane.ToString());
                throw ane;
            }
            catch (IOException ex)
            {
                if (ex.HResult == -2146232800)
                {
                    Logger.LogError("SocketException : {0}", ex.ToString());
                    throw ex;
                }
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError("Unexpected exception : {0}", e.ToString());
                throw e;
            }
            return true;
        }
        enum CONNECT_TYPE
        {
            NEW_CONNECT,
            RE_CONNECT
        }
        /// <summary>
        /// UDP HolePunchingでUDPを接続する
        /// </summary>
        /// <param name="ServerHost"></param>
        /// <param name="PortNumber"></param>
        /// <returns></returns>
        private (Socket socket, IPEndPoint point) ConnectUdp(string ServerHost, int PortNumber)
        {
            var sending_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            IPAddress host;
            if (IPAddress.TryParse(ServerHost, out host) == false)
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(ServerHost);
                host = ipHostInfo.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();
            }
            else
            {
                host = IPAddress.Parse(ServerHost);
            }

            var sending_end_point = new IPEndPoint(host, PortNumber);

            Thread.Sleep(1000);
            sending_socket.SendTo(Encoding.UTF8.GetBytes(host.ToString()), sending_end_point);

            return (sending_socket, sending_end_point);
        }

        /// <summary>
        /// UDPの接続ポート番号を受信
        /// </summary>
        /// <returns></returns>
        private int ReceiveUdpPort()
        {
            byte[] data = new byte[SocketySetting.MAX_BUFFER];
            var ns = serverSocket.GetStream();
            ns.Read(data, 0, sizeof(int));
            int port = MessagePackSerializer.Deserialize<int>(data);

            return port;
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
                clientReceiver.AbortReceiveProcess();
                serverSocket.Close();
                serverSocket = null;
            }
        }

        private ClientInfo CreateNewClientInfo(string UserName)
        {
            ClientInfo clientInfo = new ClientInfo();
            clientInfo.ClientID = Guid.NewGuid();
            clientInfo.Name = UserName;

            return clientInfo;
        }

        private void SendClientInfo(TcpClient socket, ClientInfo clientInfo)
        {
            byte[] bytes = MessagePackSerializer.Serialize(clientInfo);
            var ns = socket.GetStream();
            ns.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// サーバメソッド呼び出し(TCP)
        /// </summary>
        /// <param name="serverMethodName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] Send(string serverMethodName, byte[] data)
        {
            if (data != null && data.Length > SocketySetting.MAX_BUFFER)
            {
                throw new SocketyException(SocketyException.SOCKETY_EXCEPTION_ERROR.BUFFER_OVER);
            }
            return clientReceiver.Send(serverMethodName, data);
        }

        /// <summary>
        /// UDP送信
        /// </summary>
        /// <param name="data"></param>
        public void UdpSend(byte[] data)
        {
            if (data != null && data.Length > SocketySetting.MAX_UDP_SIZE)
            {
                throw new SocketyException(SocketyException.SOCKETY_EXCEPTION_ERROR.BUFFER_OVER);
            }
            //パケット分割
            var packets = PacketSerivce<T>.PacketSplit(clientInfo, data);

            packets.ForEach(x =>
            {
                clientReceiver.UdpSend(x);
            });
        }
    }
}
