using System;
using System.Text;
using iSocket.Client;

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
    class Work 
    {
        public void UserJoin(string JoinUserName)
        {
            Console.WriteLine($"Join: {JoinUserName}");
        }

        public void Push()
        {
            Console.WriteLine("Push!");
        }

        public void Start()
        {
            var client = new Client<Work>();


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
            client.clientReceiver.Send("Join", DateTime.Now.ToString());

            while (true)
            {
                //var ret = (string)client.clientReceiver.Send("Echo", DateTime.Now.ToString());
                //Console.WriteLine(ret);

                client.clientReceiver.UdpSend("UDP Test");
                System.Threading.Thread.Sleep(2000);
            }
        }

    }


}
