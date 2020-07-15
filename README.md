# Sockety
.NET Coreで書いたネットワークフレームワークです。
[MagicoOnion](https://github.com/Cysharp/MagicOnion)にインスパイアを受け開発しました。

# 開発経緯
.Net Coreで利用しマルチプラットフォームで通信を行う必要があり、MagicOnionを用いて通信を行っていました。
MagicOnionは通信コアにgRPCを使っており、ネイティブコードを利用しています。マルチプラットフォームで通信を行う場合、残念ながらARM版Windowsでは利用できないこと画面しました。
※ARM版Windowsでの動作はあまり必要ないと思いますがHololens2で利用するためです。
そこで一念発起しネットワークフレームワークを作成した次第です。
ですので、目指したところは"通信をラッピングし相手のメソッドを簡単に呼ぶ"です。

# 機能
1. クライアントからサーバを呼び出し戻り値を取得
1. サーバからクライアントに値を送りつける
1. サーバで受信したデータをブロードキャスト
1. UDPを利用しクライアントからサーバにデータを送る
1. UDPで受信したデータをクライアントにブロードキャストする
1. 切断検知を行い接続のリトライを行う

[UDPはHolePunchingでNATを回避しています](https://qiita.com/k-yamamoto/items/1bc295f83c873921b408)

```
Client.Connect("localhost",11000,"MyApp",this);
```

これでクライアントは接続を開始しますが、この時にTCPとUDPを張ります。
ですので、高速通信を行いたい場合はUDPを利用することも、大事なデータはTCPで送ることも出来ます
クライアント(呼ぶ側)

``` 
client.Send("Join", Encoding.ASCII.GetBytes($"{DateTime.Now.ToString()}"));
```
とやるとサーバのメソッド(この場合はJoin)を呼び、引数である現在時刻を渡すことが出来ます。

サーバ(呼ばれる側)

```
public byte[] Join(ClientInfo sendclientInfo,byte[] JoinDate)
```
サーバの受け側はこんな感じです。
ClientInfo→送信元のクライアント情報
JoinDate→client.Sendの第２引数

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

