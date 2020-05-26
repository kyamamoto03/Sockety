using iSocket.Model;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace iSocket.Client
{
    public class ClientReceiver : IDisposable
    {
        private Socket serverSocket = null;
        private Thread thread = null;

        private byte[] CommunicateButter = new byte[1024];

        public Action<ISocketPacket> ReceiveAction;

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

        public void Run(Socket handler)
        {
            serverSocket = handler;
            thread = new Thread(new ThreadStart(Process));
            thread.Start();
        }

        private void Process()
        {
            while (true)
            {
                // Encode the data string into a byte array.  
                byte[] msg = Encoding.ASCII.GetBytes(DateTime.Now.ToString());

                // Send the data through the socket.  
                int bytesSent = serverSocket.Send(msg);

                // Receive the response from the remote device.  
                int bytesRec = serverSocket.Receive(CommunicateButter);
                var packet = MessagePack.MessagePackSerializer.Deserialize<ISocketPacket>(CommunicateButter);
                ReceiveAction?.Invoke(packet);

                Thread.Sleep(2000);
            }


        }

    }
}
