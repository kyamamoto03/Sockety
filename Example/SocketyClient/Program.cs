    using Microsoft.Extensions.Logging;
using Sockety.Client;
using Sockety.Model;
using System;
using System.Text;
using System.Threading;

namespace SocketyClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Work w = new Work();
            w.Start();
        }
    }
    class Work : IService
    {
        public void UserJoin(byte[] JoinUserNameBytes)
        {
            Console.WriteLine($"Join: {Encoding.ASCII.GetString(JoinUserNameBytes)}");
        }

        public void Push()
        {
            Console.WriteLine("Push!");
        }

        Client<Work> client;
        public void Start()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                .AddConsole()
                .AddDebug();
            });
            ILogger logger = loggerFactory.CreateLogger<Program>();

            client = new Client<Work>(logger);


            ///再接続処理
            client.ConnectionReset = () =>
            {
                while (true)
                {
                    if (client.ReConnect() == true)
                    {
                        return;
                    }
                    Thread.Sleep(1000);
                }
            };

            try
            {
                client.Connect("localhost", 11000, "ConsoleApp", this);
            }catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
                return;
            }
            client.Send("Join", Encoding.ASCII.GetBytes(DateTime.Now.ToString()));
            var echoData = client.Send("Echo", Encoding.ASCII.GetBytes(DateTime.Now.ToString()));
            Console.WriteLine($"{Encoding.ASCII.GetString(echoData)}");

            while (true)
            {
                echoData = client.Send("Echo", Encoding.ASCII.GetBytes(DateTime.Now.ToString()));
                Console.WriteLine($"{Encoding.ASCII.GetString(echoData)}");
                //var t = Encoding.ASCII.GetBytes("UDP Test");
                //client.UdpSend(t);
                System.Threading.Thread.Sleep(200);
            }
        }

        public void UdpReceive(ClientInfo clientInfo, byte[] obj)
        {
            string str = Encoding.ASCII.GetString(obj);

            if (client.clientInfo.Equals(clientInfo) == false)
            {
                Console.WriteLine($"UDP Receive:{str}");
            }
            else
            {
                Console.WriteLine("自分のやつ");
            }
        }
    }


}
