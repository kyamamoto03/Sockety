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

            client.Connect("192.168.85.162", 11000,"ConsoleApp",this);
            client.Send("Join", Encoding.ASCII.GetBytes(DateTime.Now.ToString()));
            var echoData = client.Send("Echo", Encoding.ASCII.GetBytes(DateTime.Now.ToString()));
            Console.WriteLine($"{Encoding.ASCII.GetString(echoData)}");

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
