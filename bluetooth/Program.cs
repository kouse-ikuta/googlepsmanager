using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using InTheHand.Net;

class Program
{
    static void Main(string[] args)
    {
        // 接続先のBluetoothアドレスを指定
        string targetAddress = "B4:70:64:DD:AB:89";  // 接続済みBluetoothデバイスのアドレスをここに入力

        // UUIDを指定（SPPのUUID）
        Guid serviceUuid = new Guid("fa87c0d0-afac-11de-8a39-0800200c9a66");

        // Bluetoothデバイスに接続
        ConnectToServer(targetAddress, serviceUuid);
    }

    // サーバーに接続する関数（UUIDを使用）
    static void ConnectToServer(string targetAddress, Guid serviceUuid, int port = 1)
    {
        try
        {
            // Bluetoothクライアントの作成
            BluetoothClient client = new BluetoothClient();

            // Bluetoothデバイスに接続
            client.Connect(new BluetoothEndPoint(BluetoothAddress.Parse(targetAddress), serviceUuid));
            Console.WriteLine($"デバイス {targetAddress} に接続しました");

            // ネットワークストリームを取得
            var stream = client.GetStream();

            // キャンセルトークンの設定
            CancellationTokenSource cts = new CancellationTokenSource();

            // 受信タスクの開始
            Task receiveTask = Task.Run(() => ReceiveMessages(stream, cts.Token), cts.Token);

            // 送信ループの開始（メインスレッド）
            while (true)
            {
                // メッセージを送信
                Console.Write("送信するメッセージを入力してください（終了するには 'exit' と入力）: ");
                string message = Console.ReadLine();
                if (message.ToLower() == "exit")
                {
                    Console.WriteLine("接続を終了します...");
                    cts.Cancel();  // 受信タスクをキャンセル
                    break;  // "exit"と入力されたらループを抜けて終了
                }

                SendMessage(stream, message);
            }

            // 接続を閉じる
            client.Close();
            cts.Cancel();  // 念のためキャンセルを再度呼び出す
        }
        catch (Exception ex)
        {
            Console.WriteLine($"接続に失敗しました: {ex.Message}");
        }
    }

    // メッセージを送信する関数
    static void SendMessage(System.IO.Stream stream, string message)
    {
        try
        {
            byte[] messageBytes = Encoding.ASCII.GetBytes(message);
            stream.Write(messageBytes, 0, messageBytes.Length);
            Console.WriteLine($"送信: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"メッセージの送信に失敗しました: {ex.Message}");
        }
    }

    // メッセージを受信する関数
    static void ReceiveMessages(System.IO.Stream stream, CancellationToken token)
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
                        string receivedMessage = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"\nサーバーからのメッセージ: {receivedMessage}");
                        Console.Write("送信するメッセージを入力してください（終了するには 'exit' と入力）: ");
                    }
                }
                else
                {
                    Thread.Sleep(100);  // CPU負荷を下げるために少し待機
                }
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Console.WriteLine($"メッセージの受信に失敗しました: {ex.Message}");
            }
        }
    }
}
