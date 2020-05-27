using iSocket.Model;
using iSocket.Server;
using MessagePack;
using MessagePack.Resolvers;
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
        public ServerCore<ServerLoop> serverCore = new ServerCore<ServerLoop>();

        private async Task MainLoop()
        {
            serverCore.ConnectionReset = (x) =>
            {
                Console.WriteLine($"{x.Name}さんが切断されました");
            };

            Console.WriteLine("MainLoop Start.");
            //すべてのIPで接続を受け付ける
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, PortNumber);

            serverCore.Start(localEndPoint, _stoppingCts,this);

            while (!_stoppingCts.IsCancellationRequested)
            {
                int cnt = 0;
                while (true)
                {

                    if (cnt++ == 5)
                    {
                        try
                        {
                            serverCore.BroadCastNoReturn("Push", null);
                            Console.WriteLine("Push");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }
                        cnt = 0;
                    }
                    Thread.Sleep(1000);
                }
            }

        }

        public object Echo(object obj)
        {
            string data = (string)obj;
            return $"ServerEcho {data}";
        }

        public object Join(string UserJoin)
        {
            serverCore.BroadCastNoReturn("UserJoin", UserJoin);
            return null;
        }
    }
}
