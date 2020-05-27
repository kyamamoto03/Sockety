using iSocket.Model;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace iSocket.Client
{
    public class ClientReceiver<T>: IDisposable where T: ISocket
    {
        private Socket serverSocket = null;
        private Socket serverUdpSocket;
        private IPEndPoint serverUdpPort;

        private Thread TcpReceiveThread;
        private Thread UdpReceiveThread;

        private byte[] CommunicateButter = new byte[1024];
        /// <summary>
        /// 通信が切断時に発火
        /// </summary>
        public Action ConnectionReset;

        #region IDisposable
        public void Dispose()
        {
            AbortReceiveProcess();
        }
        #endregion

        public void AbortReceiveProcess()
        {
            if (TcpReceiveThread != null)
            {
                TcpReceiveThread.Abort();
                TcpReceiveThread = null;
            }

        }

        private ManualResetEvent RecieveSyncEvent = new ManualResetEvent(false);

        private T Parent;
        public void Run(Socket handler,Socket UdpSocket,IPEndPoint UdpEndPort, T parent)
        {
            Parent = parent;
            serverSocket = handler;
            serverUdpSocket = UdpSocket;
            serverUdpPort = UdpEndPort;

            TcpReceiveThread = new Thread(new ThreadStart(ReceiveProcess));
            TcpReceiveThread.Start();

            UdpReceiveThread = new Thread(new ThreadStart(UdpReceiveProcess));
            UdpReceiveThread.Start();
        }

        private string ServerCallMethodName = "";
        private object ServerResponse;

        public void UdpSend(object data)
        {
            var bytes = MessagePackSerializer.Serialize(data);

            serverUdpSocket.SendTo(bytes, SocketFlags.None, serverUdpPort);
        }

        public object Send(string serverMethodName,object data)
        {
            lock (ServerCallMethodName)
            {
                ServerCallMethodName = serverMethodName;
            }
            ISocketPacket packet = new ISocketPacket {MethodName = serverMethodName, PackData = data };
            RecieveSyncEvent.Reset();
            serverSocket.Send(MessagePackSerializer.Serialize(packet));
            RecieveSyncEvent.WaitOne();

            return ServerResponse;
        }
        private void UdpReceiveProcess()
        {
            var CommunicateBuffer = new byte[1024];

            while(true)
            {
                try
                {
                    serverUdpSocket.Receive(CommunicateBuffer);
                    var data = MessagePackSerializer.Deserialize<ISocketPacket>(CommunicateBuffer);
                    Parent.UdpReceive(data.PackData);

                }
                catch(Exception ex)
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// 受信を一括して行う
        /// </summary>
        private void ReceiveProcess()
        {
            while (true)
            {
                try
                {
                    int bytesRec = serverSocket.Receive(CommunicateButter);
                    var packet = MessagePackSerializer.Deserialize<ISocketPacket>(CommunicateButter);
                    lock (RecieveSyncEvent)
                    {
                        if (string.IsNullOrEmpty(ServerCallMethodName) != true && ServerCallMethodName == packet.MethodName)
                        {
                            ServerResponse = packet.PackData;
                            RecieveSyncEvent.Set();
                        }
                        else
                        {
                            InvokeMethod(packet);
                        }
                    }
                }catch(SocketException ex)
                { 
                    if (ex.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        //通信切断
                        Task.Run(() => ConnectionReset?.Invoke());

                        //受信スレッド終了
                        return;
                    }
                }catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                Thread.Sleep(10);
            }
        }

        private void InvokeMethod(ISocketPacket packet)
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
    }
}
