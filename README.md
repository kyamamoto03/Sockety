# Sockety
.NET Coreで書いたネットワークフレームワークです。
Socketをラッピングし、簡単にサーバ、クライアント通信を行えます。
## 通信方式
Socketyでクライアントからサーバに接続するとTCPおよびUDPを接続します。
## 機能
・切断感知（内部利用）
・クライアント→サーバ通信（サーバからの戻り値あり）
・サーバ→クライアントのプッシュ通信
・UDPを使った軽量通信
・SSLを利用した暗号化通信(TCPのみ)
・AESを使用したUDP通信
・認証トークンによる通信時の認証
です。
[UDPはHolePunching](https://qiita.com/k-yamamoto/items/1bc295f83c873921b408)でNATを回避しています

# 開発経緯
.Net Coreで利用しマルチプラットフォームで通信を行う必要があり、MagicOnionを用いて通信を行っていました。
MagicOnionは通信コアにgRPCを使っており、ネイティブコードを利用しています。マルチプラットフォームで通信を行う場合、残念ながらARM版Windowsでは利用できないこと画面しました。
※ARM版Windowsでの動作はあまり必要ないと思いますがHololens2で利用するためです。

# QuickStart
サーバ側

```
int PortNumber = 11000;
//SSL Setting
var serverSetting = new ServerSetting { UseSSL = true };
serverSetting.LoadCertificateFile("++++.pfx", "certPassword");

//Authentication Filter
var authtificationFilter = new LocalAuthentificationFilter();
serverCore.SocketyFilters.Add(authtificationFilter);

//UDP Port Setting
serverCore.InitUDP(11000, 11120);
//Sockety Start
serverCore.Start(localEndPoint: new IPEndPoint(IPAddress.Any, PortNumber);, _stoppingCts: _stoppingCts, parent: this, _serverSetting: serverSetting);
```

```
Client.Connect("localhost",11000,"MyApp",this);
```


クライアント(呼ぶ側)TCP

``` 
client.Send("Join", Encoding.ASCII.GetBytes($"{DateTime.Now.ToString()}"));
```
とやるとサーバのメソッド(この場合はJoin)を呼び、引数である現在時刻を渡すことが出来ます。

サーバ(呼ばれる側)TCP

```
public byte[] Join(ClientInfo sendclientInfo,byte[] JoinDate)
```
サーバの受け側はこんな感じです。
ClientInfo→送信元のクライアント情報
JoinDate→client.Sendの第２引数

クライアント(呼ぶ側)UDP

```
client.UdpSend(Encoding.ASCII.GetBytes(DateTime.Now.ToString()));
```
サーバ(呼ばれる側)UDP
```
public void UdpReceive(ClientInfo sender, byte[] obj)
{
    //クライアントデータを全クライアントにブロードキャスト
    serverCore.BroadCastUDPNoReturn(sender, obj);

    string str = Encoding.ASCII.GetString(obj);
}
```

# パッケージ

```
dotnet add package Sockety
```
で取得してください


# 使い方
詳細な使い方は追って記述しますが、まずはExampleを参照してください。
・Sockety(本体）
・Example/SocketyServer(サンプルサーバ）
・Example/SocketyClient(サンプルクライアント）
・Example/SocketyClientUWP(サンプルクライアント版)

