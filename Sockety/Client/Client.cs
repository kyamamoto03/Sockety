using MessagePack;
using Microsoft.Extensions.Logging;
using Sockety.Model;
using Sockety.Service;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        private static string AES_IV = @"pf69DLcGrWFyZcMK";
        private static string AES_Key = @"9Fix4L4bdGPKeKWY";

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

            //新規の接続なのでClientInfoを作成
            clientInfo = CreateNewClientInfo(UserName);

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

            clientInfo.ConnectDate = DateTime.Now;

            try
            {
                //TCP接続
                serverSocket = new TcpClient(ServerEndPoint.Address.ToString(), ServerEndPoint.Port);
                var serverSetting = ReceiveServerSetting(serverSocket.GetStream());



                Stream CommunicateStream;
                SocketyCryptService cryptService = null;
                if (serverSetting.UseSSL == true)
                {
                    SslStream sslStream = new SslStream(serverSocket.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                    sslStream.AuthenticateAsClient(ServerHost);
                    CommunicateStream = sslStream as Stream;
                    cryptService = new SocketyCryptService(AES_IV, AES_Key);
                }
                else
                {
                    CommunicateStream = serverSocket.GetStream();
                }

                Logger.LogInformation("Socket connected to {0}",
                serverSocket.ToString());

                //接続出来たらクライアント情報を送る
                SendClientInfo(CommunicateStream, clientInfo);

                //Udp接続
                var UdpInfo = ConnectUdp(CommunicateStream, ServerHost, ReceiveUdpPort(CommunicateStream),cryptService: cryptService);

                //受信スレッド作成
                clientReceiver.Run(serverSocket: serverSocket,
                    _stream: CommunicateStream,
                    UdpSocket: UdpInfo.socket,
                    UdpEndPort: UdpInfo.point,
                    clientInfo: clientInfo,
                    _logger: Logger,
                    Parent: Parent,
                    cryptService: cryptService);

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
                    Logger.LogError("IOException : {0}", ex.ToString());
                    throw ex;
                }
                return false;
            }
            catch (SocketException ex)
            {
                //Logger.LogError("SocketException : {0}", ex.ToString());
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError("Unexpected exception : {0}", e.ToString());
                //throw e;
            }
            return true;
        }

        /// <summary>
        /// 接続情報を受信(平文)
        /// </summary>
        /// <param name="networkStream"></param>
        /// <returns></returns>
        private ConnectionSetting ReceiveServerSetting(NetworkStream networkStream)
        {
            byte[] sizeb = new byte[sizeof(int)];
            networkStream.Read(sizeb, 0, sizeof(int));

            byte[] data = new byte[BitConverter.ToInt32(sizeb, 0)];
            networkStream.Read(data, 0, data.Length);
            return MessagePackSerializer.Deserialize<ConnectionSetting>(data);
        }

        enum CONNECT_TYPE
        {
            NEW_CONNECT,
            RE_CONNECT
        }


        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // 証明書受け入れ
            return true;
        }

        /// <summary>
        /// UDP HolePunchingでUDPを接続する
        /// </summary>
        /// <param name="ServerHost"></param>
        /// <param name="PortNumber"></param>
        /// <returns></returns>
        private (Socket socket, IPEndPoint point) ConnectUdp(Stream stream, string ServerHost, int PortNumber,SocketyCryptService cryptService)
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

            bool OKFlag = false;
            Task.Run(() =>
            {
                while (true)
                {
                    var packet = new SocketyPacketUDP()
                    {
                        PacketType = SocketyPacketUDP.PACKET_TYPE.CONTROL,
                        PackData = Encoding.UTF8.GetBytes(host.ToString())
                    };
                    var packetdata = MessagePackSerializer.Serialize(packet);
                    if (cryptService != null)
                    {
                        packetdata = cryptService.Encrypt(packetdata);
                    }
                    sending_socket.SendTo(packetdata, sending_end_point);
                    if (OKFlag == true)
                    {
                        return;
                    }
                    Thread.Sleep(100);
                }
            });
            byte[] OK = new byte[1];
            stream.Read(OK, 0, OK.Length);
            OKFlag = true;

            return (sending_socket, sending_end_point);
        }

        /// <summary>
        /// UDPの接続ポート番号を受信
        /// </summary>
        /// <returns></returns>
        private int ReceiveUdpPort(Stream stream)
        {
            byte[] data = new byte[SocketySetting.MAX_BUFFER];
            stream.Read(data, 0, sizeof(int));
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
                clientReceiver.Close();
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

        private void SendClientInfo(Stream stream, ClientInfo clientInfo)
        {
            byte[] bytes = MessagePackSerializer.Serialize(clientInfo);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// サーバメソッド呼び出し(TCP)
        /// </summary>
        /// <param name="serverMethodName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<byte[]> Send(string serverMethodName, byte[] data)
        {
            if (data != null && data.Length > SocketySetting.MAX_BUFFER)
            {
                throw new SocketyException(SocketyException.SOCKETY_EXCEPTION_ERROR.BUFFER_OVER);
            }
            if (clientReceiver.Connected == false)
            {
                return null;
            }
            return await clientReceiver.Send(serverMethodName, data);
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
            if (clientReceiver.Connected == false)
            {
                return ;
            }
            var packetID = Guid.NewGuid();

            var packet = new SocketyPacketUDP { MethodName = "Udp", clientInfo = clientInfo, PacketID = packetID, PacketNo = 1, PackData = data, PacketType = SocketyPacketUDP.PACKET_TYPE.DATA };
            clientReceiver.UdpSend(packet);
        }

        public void SetAuthenticationToken(string token)
        {
            clientReceiver.SetAuthenticationToken(token);
        }
    }
}
