using System;
using System.Text;
using Sockety.Client;
using Sockety.Model;

namespace iSocketClient
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
        public void UserJoin(string JoinUserName)
        {
            Console.WriteLine($"Join: {JoinUserName}");
        }

        public void Push()
        {
            Console.WriteLine("Push!");
        }

        Client<Work> client;
        public void Start()
        {
            client = new Client<Work>();


            ///再接続処理
            client.ConnectionReset = () =>
            {
                while (true)
                {
                    if (client.ReConnect() == true)
                    {
                        return;
                    }
                }
            };

            client.Connect("192.168.2.12", 11000,"ConsoleApp",this);
            client.Send("Join", DateTime.Now.ToString());

            while (true)
            {
                //var ret = (string)client.Send("Echo", DateTime.Now.ToString());
                //Console.WriteLine(ret);
                var t = Encoding.ASCII.GetBytes("UDP Test");
                client.UdpSend(t);
                System.Threading.Thread.Sleep(2000);
            }
        }

        public void UdpReceive(ClientInfo clientInfo,byte[] obj)
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
