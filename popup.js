let socket;
let port;

function appendMessage(text) {
    var messagesList = document.getElementById('messagesList');
    var li = document.createElement('li');
    li.textContent = text;
    messagesList.appendChild(li);
}

document.addEventListener('DOMContentLoaded', function() {
    var startButton = document.getElementById('startButton');
    var connectButton = document.getElementById('connectButton');
    var sendButton = document.getElementById('sendButton');
    var exitButton = document.getElementById('exitButton');
    var messageToSend = document.getElementById('messageToSend');

    startButton.addEventListener('click', function() {
        // Native Messagingを使用してC#プログラムを起動
        var hostName = "com.example.bluetooth_bridge";
        port = chrome.runtime.connectNative(hostName);

        port.onDisconnect.addListener(function() {
            console.log("C#プログラムが終了しました");
            port = null;
        });

        console.log('C#プログラムを起動しました');
        appendMessage('C#プログラムを起動しました');
    });

    connectButton.addEventListener('click', function() {
        socket = new WebSocket('ws://127.0.0.1:8181');

        socket.onopen = function() {
            console.log('WebSocket接続が確立されました');
            appendMessage('WebSocket接続が確立されました');
        };

        socket.onmessage = function(event) {
            console.log('受信メッセージ: ' + event.data);
            appendMessage('受信: ' + event.data);

            if (event.data === 'BluetoothConnected') {
                // Bluetooth接続成功時の処理をここに記述
                console.log('Bluetoothに接続されました');
                appendMessage('Bluetoothに接続されました');
            }
        };

        socket.onclose = function() {
            console.log('WebSocket接続が閉じられました');
            appendMessage('WebSocket接続が閉じられました');
        };

        socket.onerror = function(error) {
            console.log('WebSocketエラー: ' + error);
            appendMessage('WebSocketエラー');
        };
    });

    sendButton.addEventListener('click', function() {
        var message = messageToSend.value;
        if (message && socket && socket.readyState === WebSocket.OPEN) {
            socket.send(message);
            console.log('メッセージを送信しました: ' + message);
            appendMessage('送信: ' + message);
            messageToSend.value = '';
        } else {
            console.log('メッセージの送信に失敗しました');
            appendMessage('メッセージの送信に失敗しました');
        }
    });

    
});
