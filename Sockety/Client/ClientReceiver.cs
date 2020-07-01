using MessagePack;
using Microsoft.Extensions.Logging;
using Sockety.Model;
using Sockety.Service;
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
        private string ServerCallMethodName = "";
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
        PacketSerivce<T> PacketSerivce;

        internal AuthenticationToken AuthenticationToken = new AuthenticationToken();

        #region IDisposable
        public void Dispose()
        {
            AbortReceiveProcess();
        }
        #endregion


        internal void AbortReceiveProcess()
        {
            if (TcpReceiveThread != null)
            {
                TcpReceiveThread.Abort();
                TcpReceiveThread = null;
            }

        }


        internal void Run(
            TcpClient handler, 
            Stream _stream,
            Socket UdpSocket,
            IPEndPoint UdpEndPort,
            ClientInfo clientInfo, 
            T parent)
        {
            Parent = parent;
            serverSocket = handler;
            serverUdpSocket = UdpSocket;
            serverUdpPort = UdpEndPort;
            ClientInfo = clientInfo;
            Connected = true;
            stream = _stream;

            KillSW = false;
            PacketSerivce = new PacketSerivce<T>();
            PacketSerivce.SetUp(parent);

            TcpReceiveThreadFinishEvent.Reset();
            UdpReceiveThreadFinishEvent.Reset();

            TcpReceiveThread = new Thread(new ThreadStart(ReceiveProcess));
            TcpReceiveThread.Name = "ReceiveProcess";
            TcpReceiveThread.Start();

            UdpReceiveThread = new Thread(new ThreadStart(UdpReceiveProcess));
            UdpReceiveThread.Name = "UdpReceiveProcess";
            UdpReceiveThread.Start();

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

        private object SendLock = new object();
        /// <summary>
        /// サーバメソッド呼び出し（サーバのレスポンスを待つ）
        /// </summary>
        /// <param name="serverMethodName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        internal byte[] Send(string serverMethodName, byte[] data)
        {
            lock (SendLock)
            {
                if (serverSocket == null || serverSocket.Connected == false || Connected == false)
                {
                    return null;
                }

                lock (ServerCallMethodName)
                {
                    ServerCallMethodName = serverMethodName;
                }
                SocketyPacket packet = new SocketyPacket { MethodName = serverMethodName, clientInfo = ClientInfo, PackData = data,Toekn = AuthenticationToken };
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
                    //if (RecieveSyncEvent.WaitOne(1000) == false)
                    //{
                    //    System.Diagnostics.Debug.WriteLine("WaitOne Error");
                    //    throw new SocketyException(SocketyException.SOCKETY_EXCEPTION_ERROR.ERROR);
                    //}
                    RecieveSyncEvent.WaitOne();
                }

            }
            return ServerResponse;
        }

        /// <summary>
        /// UDP受信用スレッド
        /// </summary>
        private void UdpReceiveProcess()
        {
            var CommunicateBuffer = new byte[SocketySetting.MAX_BUFFER];

            while (true)
            {
                if (KillSW == true)
                {
                    System.Diagnostics.Debug.WriteLine("UdpReceiveProcess Kill");
                    UdpReceiveThreadFinishEvent.Set();
                    return;
                }
                try
                {
                    serverUdpSocket.Receive(CommunicateBuffer);
                    var packet = MessagePackSerializer.Deserialize<SocketyPacketUDP>(CommunicateBuffer);

                    Task.Run(() => PacketSerivce.ReceiverSocketyPacketUDP(packet));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                }
            }
        }

        bool KillSW = false;
        /// <summary>
        /// TCP受信用スレッド
        /// </summary>
        private void ReceiveProcess()
        {
            byte[] sizeb = new byte[sizeof(int)];
            while (true)
            {
                if (KillSW == true)
                {
                    System.Diagnostics.Debug.WriteLine("ReceiveProcess Kill");
                    TcpReceiveThreadFinishEvent.Set();
                    return;
                }
                try
                {
                    //データサイズ受信
                    int bytesRec = stream.Read(sizeb, 0, sizeof(int));
                    int size = BitConverter.ToInt32(sizeb, 0);

                    //データ領域確保
                    var buffer = new byte[size];

                    int DataSize = 0;
                    do
                    {
                        if (KillSW == true)
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
                            bytesRec = stream.Read(buffer, DataSize, size - DataSize);

                            DataSize += bytesRec;
                        }
                    } while (size > DataSize);

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
                                if (packet.SocketyPacketType == SocketyPacket.SOCKETY_PAKCET_TYPE.HaertBeat)
                                {
                                    ReceiveHeartBeat();
                                }
                                else
                                {

                                    if (string.IsNullOrEmpty(ServerCallMethodName) != true && ServerCallMethodName == packet.MethodName)
                                    {
                                        ///サーバのレスポンスを待つタイプの場合は待ちイベントをセットする
                                        ServerResponse = packet.PackData;
                                        RecieveSyncEvent.Set();
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
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (IOException ex)
                {
                    //通信切断処理
                    if (ex.HResult == -2146232800)
                    {
                        //ConnectionLost("ReceiveProcess2");

                        //受信スレッド終了
                        //return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                }
                Thread.Sleep(10);
            }
        }

        private ManualResetEvent TcpReceiveThreadFinishEvent = new ManualResetEvent(false);
        private ManualResetEvent UdpReceiveThreadFinishEvent = new ManualResetEvent(false);

        private void ConnectionLost(string LostMethod = "")
        {
            KillSW = true;
            System.Diagnostics.Debug.WriteLine($"{LostMethod}:ConnectionLost");
            
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

            //再切断呼び出し
            Task.Run(() => ConnectionReset?.Invoke());
        }

        #region HeartBeat
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
            Task.Run(() =>
            {
                while (true)
                {
                    lock (ReceiveHeartBeats)
                    {
                        var LastHeartBeat = ReceiveHeartBeats.OrderByDescending(x => x.ReceiveDate).FirstOrDefault();
                        if(LastHeartBeat != null)
                        {
                            var diff = DateTime.Now - LastHeartBeat.ReceiveDate;
                            if (diff.TotalMilliseconds > SocketySetting.HEART_BEAT_LOST_TIME)
                            {
                                ConnectionLost("SurveillanceHeartBeat");
                                //監視終了
                                return;
                            }
                        }
                    }

                    Thread.Sleep(5000);
                }
            });
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
