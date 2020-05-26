using iSocket.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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

    class Work
    {
        public MainPage Parent;
        public void UserJoin(byte[] packet)
        {
            Console.WriteLine("{0}",
                    System.Text.Encoding.ASCII.GetString(packet, 0, packet.Length));
        }
        public void Push(byte[] a)
        {
            Task.Run(async () => await Parent.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                Parent.e();
            }
            ));
        }

        public void Start()
        {
            var client = new Client<Work>();

            client.Connect("192.168.2.12", 11000,"HoloLens2", this);
            while (true)
            {
                System.Threading.Thread.Sleep(2000);
            }
        }

    }

}
