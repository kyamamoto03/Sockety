using iSocket.Model;
using MessagePack;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace iSocket.Server
{
    public class Server : IHostedService , IDisposable
    {
        #region IHostedService
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private Task _executingTask;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _executingTask = Task.Run(async () => await MainLoop());

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _stoppingCts.Cancel();
            }
            finally
            {
                await Task.WhenAny(_executingTask, Task.Delay(5000,
                                                              cancellationToken));
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (MainListener != null && MainListener.Connected == true)
            {
                MainListener.Disconnect(false);
                MainListener = null;
            }
        }
        #endregion

        private int PortNumber = 11000;
        private Socket MainListener;
        private List<ClientHub> ClientThreads = new List<ClientHub>();

        private async Task MainLoop()
        {
            Console.WriteLine("MainLoop Start.");
            //すべてのIPで接続を受け付ける
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, PortNumber);

            // メイン接続のTCP/IPを作成
            MainListener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            MainListener.Bind(localEndPoint);
            MainListener.Listen(10);

            while (!_stoppingCts.IsCancellationRequested)
            {
                Console.WriteLine("Waiting for a connection...");
                Socket handler = await MainListener.AcceptAsync();
                var clientInfo = ClientInfoReceive(handler);

                // クライアントが接続したので、受付スレッドを開始する
                var clientHub = new ClientHub(handler, clientInfo);
                clientHub.Run();

                ClientThreads.Add(clientHub);

            }
        }

        private ClientInfo ClientInfoReceive(Socket handler)
        {
            byte[] bytes = new Byte[1024];
            handler.Receive(bytes);
            ClientInfo clientInfo = MessagePackSerializer.Deserialize<ClientInfo>(bytes);

            return clientInfo;
        }
    }
}
