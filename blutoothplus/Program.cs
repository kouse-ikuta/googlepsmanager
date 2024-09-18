using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using Fleck;
using System.Collections.Generic;
using InTheHand.Net;

class Program
{
    static BluetoothClient bluetoothClient = null;
    static List<IWebSocketConnection> allSockets = new List<IWebSocketConnection>();
    static CancellationTokenSource cts = new CancellationTokenSource();
    static Guid serviceUuid = new Guid("fa87c0d0-afac-11de-8a39-0800200c9a66");  // SPPのUUID
    static ManualResetEvent exitEvent = new ManualResetEvent(false);

    static void Main(string[] args)
    {
        bool isNativeMessaging = false;

        // エラーログをファイルに出力するように設定
        System.IO.StreamWriter logFile = new System.IO.StreamWriter("log.txt", true);
        logFile.AutoFlush = true;  // 追加
        Console.SetError(logFile);
        Console.SetOut(logFile);    // 標準出力もログファイルに書き込む

        try
        {
            // Native Messagingからの起動を検知
            if (args.Length > 0 && args[0] == "--native-messaging")
            {
                isNativeMessaging = true;
                // 標準入力の終わりまで待機するタスクを開始
                Task.Run(() => WaitForParentProcessExit());
            }

            // Bluetooth接続先の情報
            string targetAddress = "B4:70:64:DD:AB:89";  // 実際のAndroidデバイスのBluetoothアドレスに置き換えてください

            // Bluetoothデバイスに接続
            var connectTask = Task.Run(() => ConnectToServer(targetAddress, serviceUuid));

            // WebSocketサーバーの開始
            StartWebSocketServer();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"プログラムの実行中にエラーが発生しました: {ex}");
        }

        if (isNativeMessaging)
        {
            // プログラムが終了するまで待機
            exitEvent.WaitOne();
        }
        else
        {
            Console.WriteLine("C#プログラムが起動しました。Enterキーで終了します。");
            Console.ReadLine();
            cts.Cancel();
            Cleanup();
        }

        // ログファイルを閉じる
        logFile.Close();
    }

    // 標準入力の終わりを検知する関数
    static void WaitForParentProcessExit()
    {
        try
        {
            while (Console.In.Read() != -1)
            {
                // 標準入力が閉じられるまで待機
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"標準入力の読み取り中にエラーが発生しました: {ex}");
        }
        finally
        {
            Console.WriteLine("標準入力が閉じられました。プログラムを終了します。");
            cts.Cancel();
            Cleanup();
            exitEvent.Set();  // メインスレッドを解放
        }
    }

    // Bluetoothデバイスに接続する関数
    static void ConnectToServer(string targetAddress, Guid serviceUuid)
    {
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                bluetoothClient = new BluetoothClient();
                bluetoothClient.Connect(new BluetoothEndPoint(BluetoothAddress.Parse(targetAddress), serviceUuid));
                Console.WriteLine($"デバイス {targetAddress} にBluetoothで接続しました");

                // **Bluetooth接続成功時にJavaScriptにメッセージを送信**
                SendMessageToWebSockets("BluetoothConnected");

                // Bluetooth接続後にメッセージ受信を開始
                var stream = bluetoothClient.GetStream();
                Task.Run(() => ReceiveBluetoothMessages(stream, cts.Token), cts.Token);
                break;  // 接続成功でループを抜ける
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Bluetooth接続に失敗しました: {ex}");
                // 再接続を試みる
                Task.Delay(5000).Wait();
            }
        }
    }

    // WebSocketサーバーの開始
    static void StartWebSocketServer()
    {
        try
        {
            FleckLog.Level = LogLevel.Debug;  // ログレベルをDebugに設定
            var server = new WebSocketServer("ws://127.0.0.1:8181");

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("WebSocket接続が確立されました");
                    allSockets.Add(socket);
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("WebSocket接続が閉じられました");
                    allSockets.Remove(socket);
                };
                socket.OnMessage = message =>
                {
                    Console.WriteLine("WebSocketで受信したメッセージ: " + message);

                    if (message == "exit")
                    {
                        Console.WriteLine("終了コマンドを受信しました。プログラムを終了します。");
                        cts.Cancel();
                        Cleanup();
                        exitEvent.Set();  // メインスレッドを解放
                    }
                    else
                    {
                        // Bluetooth経由でAndroidにメッセージを送信
                        if (bluetoothClient != null && bluetoothClient.Connected)
                        {
                            SendBluetoothMessage(bluetoothClient.GetStream(), message);
                        }
                        else
                        {
                            Console.WriteLine("Bluetoothに接続されていません。メッセージを送信できません。");
                        }
                    }
                };
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WebSocketサーバーの起動に失敗しました: {ex}");
        }
    }

    // WebSocketを通じてJavaScriptにメッセージを送信する関数
    static void SendMessageToWebSockets(string message)
    {
        foreach (var socket in allSockets)
        {
            if (socket.IsAvailable)
            {
                socket.Send(message);
                Console.WriteLine($"WebSocket経由で送信: {message}");
            }
        }
    }

    // Bluetooth経由でメッセージを送信する関数
    static void SendBluetoothMessage(System.IO.Stream stream, string message)
    {
        try
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            stream.Write(messageBytes, 0, messageBytes.Length);
            Console.WriteLine($"Bluetooth経由で送信: {message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"メッセージの送信に失敗しました: {ex}");
        }
    }

    // Bluetooth経由でメッセージを受信する関数
    static void ReceiveBluetoothMessages(System.IO.Stream stream, CancellationToken token)
    {
        try
        {
            byte[] buffer = new byte[1024];
            while (!token.IsCancellationRequested)
            {
                if (stream.CanRead)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"Bluetoothから受信したメッセージ: {receivedMessage}");

                        // 受信したメッセージをWebSocket経由でJavaScriptに送信
                        SendMessageToWebSockets(receivedMessage);
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Console.Error.WriteLine($"Bluetoothメッセージの受信に失敗しました: {ex}");
            }
        }
    }

    // リソースの解放を行う関数
    static void Cleanup()
    {
        try
        {
            cts.Cancel();

            // Bluetooth接続のクローズ
            if (bluetoothClient != null)
            {
                bluetoothClient.Close();
                bluetoothClient.Dispose();
                bluetoothClient = null;
            }

            // WebSocket接続のクローズ
            foreach (var socket in allSockets)
            {
                if (socket.IsAvailable)
                {
                    socket.Close();
                }
            }
            allSockets.Clear();

            Console.WriteLine("リソースを解放しました。");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"リソースの解放中にエラーが発生しました: {ex}");
        }
    }
}
