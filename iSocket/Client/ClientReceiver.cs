using iSocket.Model;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace iSocket.Client
{
    public class ClientReceiver<T> : IDisposable
    {
        private Socket serverSocket = null;
        private Thread thread = null;

        private byte[] CommunicateButter = new byte[1024];

        #region IDisposable
        public void Dispose()
        {
            if (thread != null)
            {
                thread.Abort();
                thread = null;
            }
        }
        #endregion

        private ManualResetEvent RecieveSyncEvent = new ManualResetEvent(false);

        private T Parent;
        public void Run(Socket handler, T parent)
        {
            Parent = parent;
            serverSocket = handler;
            thread = new Thread(new ThreadStart(ReceiveProcess));
            thread.Start();
        }

        private string ServerCallMethodName = "";
        private byte[] ServerResponse;

        public byte[] Send(string serverMethodName,byte[] data)
        {
            lock (ServerCallMethodName)
            {
                ServerCallMethodName = serverMethodName;
            }
            ISocketPacket packet = new ISocketPacket { PackData = data };
            RecieveSyncEvent.Reset();
            serverSocket.Send(MessagePackSerializer.Serialize(packet));
            RecieveSyncEvent.WaitOne();

            return ServerResponse;
        }

        private void ReceiveProcess()
        {
            while (true)
            {
                // Receive the response from the remote device.  
                int bytesRec = serverSocket.Receive(CommunicateButter);
                var packet = MessagePack.MessagePackSerializer.Deserialize<ISocketPacket>(CommunicateButter);
                lock(RecieveSyncEvent)
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
            Task.Run(() => method.Invoke(Parent, new object[] { packet.PackData }));
        }
    }
}
