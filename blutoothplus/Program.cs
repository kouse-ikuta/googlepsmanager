using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using Fleck;
using System.Collections.Generic;
using InTheHand.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

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
        logFile.AutoFlush = true;
        Console.SetError(logFile);
        Console.SetOut(logFile);

        try
        {
            if (args.Length > 0 && args[0] == "--native-messaging")
            {
                isNativeMessaging = true;
                Task.Run(() => WaitForParentProcessExit());
            }

            // WebSocketサーバーの開始（先にWebSocket接続を確立）
            StartWebSocketServer();


            // WebSocketクライアントが接続されるまで待機
            while (allSockets.Count == 0)
            {
                Task.Delay(100).Wait();  // 100msの遅延を入れてループを繰り返す
            }


            SendMessageToWebSockets("ここまでは行けてます。");
            // Bluetoothデバイスの情報を取得してWebSocketで送信
            GetAndSendBluetoothDevices();  // WebSocket接続後にBluetoothデバイス情報の取得と送信を開始
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"プログラムの実行中にエラーが発生しました: {ex}");
        }

        if (isNativeMessaging)
        {
            exitEvent.WaitOne();
        }
        else
        {
            Console.WriteLine("C#プログラムが起動しました。Enterキーで終了します。");
            Console.ReadLine();
            cts.Cancel();
            Cleanup();
        }

        logFile.Close();
    }

    // **Bluetoothデバイスの情報を取得して送信する関数**
    // **Bluetoothデバイスの情報を取得して送信する関数**
    static void GetAndSendBluetoothDevices()
    {
        Console.WriteLine($"GetAndSendBluetoothDevicesの起動を確認");

        try
        {
            // Bluetoothデバイス検索パラメータの設定
            BLUETOOTH_DEVICE_SEARCH_PARAMS searchParams = new BLUETOOTH_DEVICE_SEARCH_PARAMS();
            searchParams.dwSize = Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_SEARCH_PARAMS));
            searchParams.fReturnAuthenticated = true;
            searchParams.fReturnRemembered = true;
            searchParams.fReturnConnected = true;  // 接続中のデバイスのみを取得
            searchParams.fReturnUnknown = true;
            searchParams.fIssueInquiry = false;    // スキャンを行わない
            searchParams.hRadio = IntPtr.Zero;     // すべてのBluetoothラジオを対象にする

            // デバイス情報構造体の初期化
            BLUETOOTH_DEVICE_INFO deviceInfo = new BLUETOOTH_DEVICE_INFO();
            deviceInfo.dwSize = Marshal.SizeOf(typeof(BLUETOOTH_DEVICE_INFO));

            // Bluetoothデバイス情報を格納するリスト
            List<string> deviceInfoList = new List<string>();

            // Bluetoothデバイスの最初の検索を開始
            IntPtr hFind = BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);

            if (hFind != IntPtr.Zero)
            {
                do
                {
                    if (deviceInfo.fConnected)  // デバイスが接続中かを確認
                    {
                        string deviceInfoStr = $"Name: {deviceInfo.szName}, Address: {deviceInfo.Address:X}";
                        Console.WriteLine(deviceInfoStr);

                        // デバイス情報をリストに追加
                        deviceInfoList.Add(deviceInfoStr);
                    }
                }
                while (BluetoothFindNextDevice(hFind, ref deviceInfo));  // 次のデバイスを検索

                // 検索を終了
                BluetoothFindDeviceClose(hFind);

                if (deviceInfoList.Count > 0)
                {
                    // すべてのデバイス情報を1つのメッセージにまとめる
                    string combinedMessage = string.Join("\n", deviceInfoList);
                    Console.WriteLine($"まとめて送信: \n{combinedMessage}");

                    // WebSocketでまとめてデバイス情報を送信
                    SendMessageToWebSockets(combinedMessage);
                }
                else
                {
                    Console.WriteLine("接続中のBluetoothデバイスはありません。");
                }
            }
            else
            {
                Console.WriteLine("接続中のBluetoothデバイスはありません。");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bluetoothデバイス情報の取得中にエラーが発生しました: {ex}");
        }
    }


    // BLUETOOTH_DEVICE_SEARCH_PARAMS 構造体
    [StructLayout(LayoutKind.Sequential)]
    public struct BLUETOOTH_DEVICE_SEARCH_PARAMS
    {
        public int dwSize;
        public bool fReturnAuthenticated;
        public bool fReturnRemembered;
        public bool fReturnUnknown;
        public bool fReturnConnected;
        public bool fIssueInquiry;
        public byte cTimeoutMultiplier;
        public IntPtr hRadio;
    }

    // BLUETOOTH_DEVICE_INFO 構造体
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BLUETOOTH_DEVICE_INFO
    {
        public int dwSize;
        public long Address;
        public uint ulClassofDevice;
        public bool fConnected;
        public bool fRemembered;
        public bool fAuthenticated;
        public SystemTime stLastSeen;
        public SystemTime stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    // SystemTime 構造体
    [StructLayout(LayoutKind.Sequential)]
    public struct SystemTime
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;
    }

    // Windows API 関数のインポート
    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode)]
    public static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS searchParams, ref BLUETOOTH_DEVICE_INFO deviceInfo);

    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode)]
    public static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO deviceInfo);

    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode)]
    public static extern bool BluetoothFindDeviceClose(IntPtr hFind);
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

                    // **接続が確立されたらメッセージを送信**
                    string initialMessage = "WebSocket接続が確立されました";
                    socket.Send(initialMessage);  // ここでメッセージを送信
                    Console.WriteLine($"WebSocket経由で送信: {initialMessage}");
                };

                socket.OnClose = () =>
                {
                    Console.WriteLine("WebSocket接続が閉じられました");
                    allSockets.Remove(socket);
                };

                socket.OnMessage = message =>
                {
                    Console.WriteLine("WebSocketで受信したメッセージ: " + message);

                    // **WebSocketでMACアドレスを受信したらBluetooth接続**
                    if (message.StartsWith("MAC:"))
                    {
                        string macAddress = message.Substring(4);  // "MAC:"部分を除去
                        ConnectToServer(macAddress, serviceUuid);  // MACアドレスを使用してBluetooth接続
                    }
                    else
                    {
                        // **WebSocket経由でメッセージを受信し、Bluetoothデバイスに送信**
                        SendBluetoothMessage(message);
                    }
                };
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WebSocketサーバーの起動に失敗しました: {ex}");
        }
    }

    // **Bluetooth経由でメッセージを送信する関数**
    static void SendBluetoothMessage(string message)
    {
        if (bluetoothClient != null && bluetoothClient.Connected)
        {
            try
            {
                var stream = bluetoothClient.GetStream();
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                stream.Write(messageBytes, 0, messageBytes.Length);
                Console.WriteLine($"Bluetooth経由でメッセージを送信しました: {message}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Bluetoothメッセージ送信中にエラーが発生しました: {ex}");
            }
        }
        else
        {
            Console.WriteLine("Bluetoothに接続されていません。メッセージを送信できません。");
        }
    }



    // WebSocketを通じてJavaScriptにメッセージを送信する関数
    static void SendMessageToWebSockets(string message)
    {
        foreach (var socket in allSockets)
        {
            if (socket.IsAvailable)
            {
                try
                {
                    socket.Send(message);
                    Console.WriteLine($"WebSocket経由で送信: {message}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"メッセージの送信中にエラーが発生しました: {ex.Message}");
                }
            }
        }
    }



    // **Bluetoothデバイスに接続する関数**
    static void ConnectToServer(string targetAddress, Guid serviceUuid)
    {
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                bluetoothClient = new BluetoothClient();
                bluetoothClient.Connect(new BluetoothEndPoint(BluetoothAddress.Parse(targetAddress), serviceUuid));
                Console.WriteLine($"デバイス {targetAddress} にBluetoothで接続しました");

                SendMessageToWebSockets("BluetoothConnected");

                var stream = bluetoothClient.GetStream();
                Task.Run(() => ReceiveBluetoothMessages(stream, cts.Token), cts.Token);
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Bluetooth接続に失敗しました: {ex}");
                Task.Delay(5000).Wait();
            }
        }
    }

    // **Bluetooth経由でメッセージを受信する関数**
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

    // **リソースの解放を行う関数**
    static void Cleanup()
    {
        try
        {
            cts.Cancel();

            if (bluetoothClient != null)
            {
                bluetoothClient.Close();
                bluetoothClient.Dispose();
                bluetoothClient = null;
            }

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

    // **標準入力の終わりを検知する関数**
    static void WaitForParentProcessExit()
    {
        try
        {
            while (Console.In.Read() != -1)
            {
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
            exitEvent.Set();
        }
    }
}
