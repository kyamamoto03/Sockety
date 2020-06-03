using Microsoft.Extensions.Logging;
using Sockety.Client;
using Sockety.Model;
using System;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace iSocketClientUWP
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        Work w = new Work();
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Work w = new Work();
            w.Parent = this;
            Task.Run(() => w.Start());
        }

        public void e()
        {
            listBox.Items.Add($"Push:{DateTime.Now}");
        }
    }

    class Work : IService
    {
        public MainPage Parent;

        public void UserJoin(byte[] JoinUserNameBytes)
        {
            Console.WriteLine($"Join: {Encoding.ASCII.GetString(JoinUserNameBytes)}");
        }

        public void Push()
        {
            Task.Run(async () => await Parent.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Parent.e();
            }
            ));
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
            ILogger logger = loggerFactory.CreateLogger<Work>();
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
                }
            };

            client.Connect("localhost", 11000, "ConsoleApp", this);
            client.Send("Join", Encoding.ASCII.GetBytes(DateTime.Now.ToString()));

            while (true)
            {
                //var ret = (string)client.Send("Echo", DateTime.Now.ToString());
                //Console.WriteLine(ret);
                var t = Encoding.ASCII.GetBytes("UDP Test");
                client.UdpSend(t);
                System.Threading.Thread.Sleep(2000);
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
