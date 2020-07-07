using MessagePack;
using Microsoft.Extensions.Logging;
using Sockety.Base;
using Sockety.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Sockety.Client
{
    internal class ClientReceiver<T> : IDisposable where T : IService
    {
        private TcpClient serverSocket = null;
        private Stream stream;
        private Socket serverUdpSocket;
        private IPEndPoint serverUdpPort;

        public bool Connected = false;
        /// <summary>
        /// TCPの受信用スレッド
        /// </summary>
        private Thread TcpReceiveThread;
        /// <summary>
        /// UDPの受信用スレッド
        /// </summary>
        private Thread UdpReceiveThread;

        /// <summary>
        /// 通信が切断時に発火
        /// </summary>
        internal Action ConnectionReset;

        /// <summary>
        /// クライアントが読んでいるサーバのメソッド名
        /// </summary>
        private ServerCall serverCall = new ServerCall();
        class ServerCall
        {
            public string ServerCallMethodName { get; private set; } = String.Empty;
            public string ServerCallMethodID { get; private set; } = String.Empty;

            public ServerCall()
            {

            }
            public ServerCall(string ServerCallMethodName, string ServerCallMethodID)
            {
                Set(ServerCallMethodName, ServerCallMethodID);
            }
            public enum RESPONOSE_TYPE
            {
                MATCH,
                WRONG_METHOD_ID,
                OTHER_METHOD
            }
            public RESPONOSE_TYPE IsType(object obj)
            {
                ServerCall c = obj as ServerCall;
                if (c.ServerCallMethodName == ServerCallMethodName && c.ServerCallMethodID == ServerCallMethodID && IsCalled == true)
                {
                    return RESPONOSE_TYPE.MATCH;
                }
                else if (c.ServerCallMethodName == ServerCallMethodName && c.ServerCallMethodID != ServerCallMethodID && IsCalled == true)
                {
                    return RESPONOSE_TYPE.WRONG_METHOD_ID;
                }
                return RESPONOSE_TYPE.OTHER_METHOD;;
            }

            public void Set(string ServerCallMethodName, string ServerCallMethodID)
            {
                this.ServerCallMethodName = ServerCallMethodName;
                this.ServerCallMethodID = ServerCallMethodID;
                _IsCalled = true;
            }

            public void Clear()
            {
                ServerCallMethodName = String.Empty;
                ServerCallMethodID = String.Empty;
                _IsCalled = false;
            }
            private bool _IsCalled = false;
            public bool IsCalled => _IsCalled;
        }
        /// <summary>
        /// サーバからのレスポンスデータ
        /// </summary>
        private byte[] ServerResponse;

        private ClientInfo ClientInfo;
        /// <summary>
        /// サーバメソッド呼び出しの待ちイベント
        /// </summary>
        private ManualResetEvent RecieveSyncEvent = new ManualResetEvent(false);

        private T Parent;

        internal AuthenticationToken AuthenticationToken = new AuthenticationToken();

        /// <summary>
        /// TCP,UDP受信スレッドキャンセルトークン
        /// </summary>
        private CancellationTokenSource ThreadCancellationToken;

        /// <summary>
        /// TCP受信スレッド終了イベント
        /// </summary>
        private ManualResetEvent TcpReceiveThreadFinishEvent = new ManualResetEvent(false);
        /// <summary>
        /// UDP受信スレッド終了イベント
        /// </summary>
        private ManualResetEvent UdpReceiveThreadFinishEvent = new ManualResetEvent(false);
        /// <summary>
        /// TCP排他ロック
        /// </summary>
        private AsyncLock TCPReceiveLock = new AsyncLock();

        private ILogger Logger;

        #region IDisposable
        public void Dispose()
        {
            Close();
        }
        #endregion



        internal void Run(
            TcpClient handler,
            Stream _stream,
            Socket UdpSocket,
            IPEndPoint UdpEndPort,
            ClientInfo clientInfo,
            ILogger _logger,
            T parent)
        {
            Parent = parent;
            serverSocket = handler;
            serverUdpSocket = UdpSocket;
            serverUdpPort = UdpEndPort;
            ClientInfo = clientInfo;
            Connected = true;
            stream = _stream;
            this.Logger = _logger;

            ThreadCancellationToken = new CancellationTokenSource();

            TcpReceiveThreadFinishEvent.Reset();
            UdpReceiveThreadFinishEvent.Reset();

            TcpReceiveThread = new Thread(new ThreadStart(ReceiveProcess));
            TcpReceiveThread.Name = "ReceiveProcess";
            TcpReceiveThread.Start();

            UdpReceiveThread = new Thread(new ThreadStart(UdpReceiveProcess));
            UdpReceiveThread.Name = "UdpReceiveProcess";
            UdpReceiveThread.Start();

            MakeHeartBeat();
            SurveillanceHeartBeat();
        }

        object serverUdpSocketlock = new object();
        /// <summary>
        /// UDP送信
        /// </summary>
        /// <param name="data"></param>
        internal void UdpSend(SocketyPacketUDP packet)
        {
            lock (serverUdpSocketlock)
            {
                if (serverUdpSocket == null)
                {
                    return;
                }

                var bytes = MessagePackSerializer.Serialize(packet);
                serverUdpSocket.SendTo(bytes, SocketFlags.None, serverUdpPort);
            }
        }

        /// <summary>
        /// サーバメソッド呼び出し（サーバのレスポンスを待つ）
        /// </summary>
        /// <param name="serverMethodName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        internal async Task<byte[]> Send(string serverMethodName, byte[] data)
        {
            using (await TCPReceiveLock.LockAsync())
            {
                if (serverSocket == null || serverSocket.Connected == false || Connected == false)
                {
                    return null;
                }

                lock (serverCall)
                {
                    serverCall.Set(ServerCallMethodName: serverMethodName,
                        ServerCallMethodID: Guid.NewGuid().ToString());
                }
                SocketyPacket packet = new SocketyPacket { MethodName = serverCall.ServerCallMethodName, MethodID = serverCall.ServerCallMethodID, clientInfo = ClientInfo, PackData = data, Toekn = AuthenticationToken };
                RecieveSyncEvent.Reset();
                ServerResponse = null;

                var d = MessagePackSerializer.Serialize(packet);
                var sizeb = BitConverter.GetBytes(d.Length);
                try
                {
                    stream.Write(sizeb, 0, sizeof(int));

                    stream.Write(d, 0, d.Length);
                }
                catch { }
                finally
                {
                    if (RecieveSyncEvent.WaitOne(SocketySetting.SERVER_RESPONSE_TIME_OUT) == false)
                    {
                        throw new SocketyException(SocketyException.SOCKETY_EXCEPTION_ERROR.SERVER_RESPONSE_ERROR);
                    }
                }

            }
            return ServerResponse;
        }

        /// <summary>
        /// UDP受信用スレッド
        /// </summary>
        private void UdpReceiveProcess()
        {

            while (true)
            {
                if (ThreadCancellationToken.Token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("UdpReceiveProcess Kill");
                    UdpReceiveThreadFinishEvent.Set();
                    return;
                }
                try
                {
                    var CommunicateBuffer = new byte[SocketySetting.MAX_UDP_SIZE];
                    serverUdpSocket.Receive(CommunicateBuffer);
                    var packet = MessagePackSerializer.Deserialize<SocketyPacketUDP>(CommunicateBuffer);

                    Task.Run(() => Parent.UdpReceive(packet.clientInfo, packet.PackData));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                }
            }
        }

        /// <summary>
        /// TCP受信用スレッド
        /// </summary>
        private void ReceiveProcess()
        {
            byte[] sizeb = new byte[8];
            while (true)
            {
                if (ThreadCancellationToken.Token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("ReceiveProcess Kill");
                    TcpReceiveThreadFinishEvent.Set();
                    return;
                }
                try
                {
                    //データサイズ受信
                    int bytesRec = stream.Read(sizeb, 0, sizeb.Length);
                    var packetSize = PacketSize.FromBytes(sizeb);

                    if (packetSize != null && packetSize.Size < SocketySetting.MAX_BUFFER)
                    {
                        //データ領域確保
                        var buffer = new byte[packetSize.Size];

                        int DataSize = 0;
                        do
                        {
                            if (ThreadCancellationToken.Token.IsCancellationRequested == true)
                            {
                                System.Diagnostics.Debug.WriteLine("ReceiveProcess Kill2");
                                TcpReceiveThreadFinishEvent.Set();
                                return;
                            }

                            if (serverSocket.Connected == false || Connected == false)
                            {
                                System.Diagnostics.Debug.WriteLine("Break");
                                TcpReceiveThreadFinishEvent.Set();
                                return;
                            }
                            else
                            {
                                //データ受信
                                bytesRec = stream.Read(buffer, DataSize, packetSize.Size - DataSize);

                                DataSize += bytesRec;
                            }
                        } while (packetSize.Size > DataSize);

                        if (bytesRec > 0)
                        {
                            SocketyPacket packet = null;
                            try
                            {
                                packet = MessagePackSerializer.Deserialize<SocketyPacket>(buffer);
                            }
                            catch
                            {
                                System.Diagnostics.Debug.WriteLine("MessagePack Fail");
                                packet = null;
                            }
                            if (packet != null)
                            {
                                lock (RecieveSyncEvent)
                                {
                                    lock (serverCall)
                                    {
                                        if (packet.SocketyPacketType == SocketyPacket.SOCKETY_PAKCET_TYPE.HaertBeat)
                                        {
                                            ReceiveHeartBeat();
                                        }
                                        else
                                        {
                                            var s = new ServerCall(packet.MethodName, packet.MethodID);
                                            if (serverCall.IsType(s) == ServerCall.RESPONOSE_TYPE.MATCH)
                                            {
                                                ///サーバのレスポンスを待つタイプの場合は待ちイベントをセットする
                                                ServerResponse = packet.PackData;
                                                RecieveSyncEvent.Set();
                                                serverCall.Clear();
                                            }
                                            else if (serverCall.IsType(s) == ServerCall.RESPONOSE_TYPE.WRONG_METHOD_ID)
                                            {
                                                //欲しいレスポンスではないので捨てる
                                                Logger.LogInformation("欲しいレスポンスではないので捨てる");
                                            }
                                            else
                                            {
                                                ///非同期なので、クライアントメソッドを呼ぶ
                                                InvokeMethod(packet);
                                            }
                                        }
                                    }
                                }
                            }

                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                    else
                    {
                        Logger.LogError("PacketSize Error");
                    }
                }
                catch (IOException ex)
                {
                    Logger.LogInformation(ex.ToString());
                }
                catch (Exception ex)
                {
                    Logger.LogInformation(ex.ToString());
                }
                Thread.Sleep(10);
            }
        }


        private void ConnectionLost(string LostMethod = "")
        {
            Close();

            //再切断呼び出し
            Task.Run(() => ConnectionReset?.Invoke());
        }

        internal void Close()
        {
            SendFinishHeartBeat().Wait();

            ThreadCancellationToken.Cancel();
            System.Diagnostics.Debug.WriteLine($"ClientReceiver:Close");

            Connected = false;
            RecieveSyncEvent.Set();
            lock (serverUdpSocketlock)
            {
                serverUdpSocket.Close();
                serverUdpSocket = null;
            }
            stream.Close();

            System.Diagnostics.Debug.WriteLine("Thread終了待ち");
            TcpReceiveThreadFinishEvent.WaitOne();
            UdpReceiveThreadFinishEvent.WaitOne();
            System.Diagnostics.Debug.WriteLine("Thread終了");


        }


        #region HeartBeat
        private void MakeHeartBeat()
        {
            Task.Run(async () =>
            {
                while (!ThreadCancellationToken.Token.IsCancellationRequested)
                {
                    await SendHeartBeat();
                    Thread.Sleep(1000);
                }
            });
        }
        private async Task SendHeartBeat()
        {
            try
            {
                using (await TCPReceiveLock.LockAsync())
                {
                    var packet = new SocketyPacket() { SocketyPacketType = SocketyPacket.SOCKETY_PAKCET_TYPE.HaertBeat };
                    var d = MessagePackSerializer.Serialize(packet);
                    var sizeb = BitConverter.GetBytes(d.Length);
                    stream.Write(sizeb, 0, sizeof(int));
                    stream.Write(d, 0, d.Length);
                }
            }
            catch (IOException ex)
            {
                Logger.LogInformation("SendHeartBeat:DisConnect");
                //await ConnectionLost();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task SendFinishHeartBeat()
        {
            try
            {
                using (await TCPReceiveLock.LockAsync())
                {
                    var packet = new SocketyPacket() { SocketyPacketType = SocketyPacket.SOCKETY_PAKCET_TYPE.FinishHeartBeat };
                    var d = MessagePackSerializer.Serialize(packet);
                    var sizeb = BitConverter.GetBytes(d.Length);
                    stream.Write(sizeb, 0, sizeof(int));
                    stream.Write(d, 0, d.Length);
                }
            }
            catch (IOException ex)
            {
                Logger.LogInformation("SendHeartBeat:DisConnect");
                //await ConnectionLost();
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

        private void SurveillanceHeartBeat()
        {
            lock (ReceiveHeartBeats)
            {
                ReceiveHeartBeats.Clear();
            }
            var thread = new Thread(new ThreadStart(SurveillanceHeartBeatProcess));
            thread.Name = "SurveillanceHeartBeatProcess";
            thread.Start();

        }

        private void SurveillanceHeartBeatProcess()
        {
            while (!ThreadCancellationToken.IsCancellationRequested)
            {
                lock (ReceiveHeartBeats)
                {
                    var LastHeartBeat = ReceiveHeartBeats.OrderByDescending(x => x.ReceiveDate).FirstOrDefault();
                    if (LastHeartBeat != null)
                    {
                        var diff = DateTime.Now - LastHeartBeat.ReceiveDate;
                        if (diff.TotalMilliseconds > SocketySetting.HEART_BEAT_LOST_TIME)
                        {
                            if (!ThreadCancellationToken.IsCancellationRequested)
                            {
                                ConnectionLost("SurveillanceHeartBeat");
                            }
                            //監視終了
                            return;
                        }
                    }
                }

                Thread.Sleep(1000);
            }

        }

        #endregion
        /// <summary>
        /// SocketyPacket.MethodNameのメソッド呼び出し
        /// </summary>
        /// <param name="packet"></param>
        private void InvokeMethod(SocketyPacket packet)
        {
            Type t = Parent.GetType();
            var method = t.GetMethod(packet.MethodName);

            if (method == null)
            {
                throw new Exception("not found Method");
            }
            if (packet.PackData != null)
            {
                Task.Run(() => method.Invoke(Parent, new object[] { packet.PackData }));
            }
            else
            {
                Task.Run(() => method.Invoke(Parent, null));
            }
        }

        public void SetAuthenticationToken(string token)
        {
            AuthenticationToken.Toekn = token;
        }

    }
}
