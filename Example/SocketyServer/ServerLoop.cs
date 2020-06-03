using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sockety.Model;
using Sockety.Server;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace iSocketServer
{
    class ServerLoop : IHostedService, IDisposable, IService
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
            if (serverCore != null)
            {
                serverCore.Dispose();
            }
        }
        #endregion

        private ILogger Logger;
        private int PortNumber = 11000;
        public ServerCore<ServerLoop> serverCore;

        private List<Group> ClientGroups = new List<Group>();

        public ServerLoop(ILogger<ServerLoop> logger)
        {
            Logger = logger;
        }
        private async Task MainLoop()
        {
            serverCore = new ServerCore<ServerLoop>(Logger);
            //グループを作成
            var group = Group.Create();
            ClientGroups.Add(group);

            serverCore.ConnectionReset = (x) =>
            {
                Logger.LogInformation($"{x.Name}さんが切断されました");
            };

            Console.WriteLine("MainLoop Start.");
            //すべてのIPで接続を受け付ける
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, PortNumber);

            serverCore.Start(localEndPoint, _stoppingCts, this);

            int cnt = 0;
            while (!_stoppingCts.IsCancellationRequested)
            {

                if (cnt++ == 5)
                {
                    try
                    {
                        serverCore.BroadCastNoReturn("Push", null);
                        Logger.LogInformation("Push");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInformation(ex.ToString());
                    }
                    cnt = 0;
                }
                Thread.Sleep(1000);
            }

        }

        public byte[] Echo(ClientInfo sendclientInfo, byte[] obj)
        {
            string data = Encoding.ASCII.GetString(obj);
            return Encoding.ASCII.GetBytes($"ServerEcho {data}");
        }

        public void Join(ClientInfo sendclientInfo, byte[] JoinDate)
        {
            //Joinしてきたクライアントにグループを追加
            sendclientInfo.JoinGroups.Add(ClientGroups[0]);

            serverCore.BroadCastNoReturn("UserJoin", JoinDate, ClientGroups);
        }

        public void UdpReceive(ClientInfo sender, byte[] obj)
        {
            //ブロードキャスト
            serverCore.BroadCastUDPNoReturn(sender, obj);

            string str = Encoding.ASCII.GetString(obj);
            Logger.LogInformation($"UDP Receive sender:{sender.ClientID} data:{str}");
        }
    }
}
