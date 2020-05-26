using iSocket.Model;
using iSocket.Server;
using MessagePack;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace iSocketServer
{
    class ServerLoop : IHostedService, IDisposable
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
            if (serverCore != null )
            {
                serverCore.Dispose();
                serverCore = null;
            }
        }
        #endregion

        private int PortNumber = 11000;
        public ServerCore serverCore = new ServerCore();

        private async Task MainLoop()
        {
            Console.WriteLine("MainLoop Start.");
            //すべてのIPで接続を受け付ける
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, PortNumber);

            serverCore.Start(localEndPoint, _stoppingCts);

            while (!_stoppingCts.IsCancellationRequested)
            {
                int cnt = 0;
                while (true)
                {

                    if (cnt++ == 5)
                    {
                        try
                        {
                            serverCore.BroadCastNoReturn("Push", new byte[1] { 0xff });
                            Console.WriteLine("Push");
                        }catch(Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }
                        cnt = 0;
                    }
                    Thread.Sleep(1000);
                }
            }

        }

    }
}
