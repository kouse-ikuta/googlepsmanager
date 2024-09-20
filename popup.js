// WebSocketとNative Messaging用の変数
let socket; // WebSocketの接続を保持する変数
let port;   // Native Messagingのポートを保持する変数

// メッセージを画面に表示する関数（必要に応じて）
function appendMessage(text) {
    console.log(text); // デバッグ用にコンソールにも出力
}

// ページが読み込まれたときのイベントハンドラ
document.addEventListener('DOMContentLoaded', function () {
    const credentialsListElement = document.getElementById('credentialsList');
    const clearAllButton = document.getElementById('clearAllButton');
    const testMessageButton = document.getElementById('testMessageButton');
    const form = document.getElementById('textForm'); // フォームを取得
    const responseArea = document.getElementById('responseArea'); // 応答表示エリア

    // C#プログラムを起動し、WebSocket接続を開始
    startBluetoothBridge();

    // 保存されたデータを取得して表示
    chrome.storage.local.get({ credentials: [] }, function (result) {
        const credentials = result.credentials;

        if (credentials.length === 0) {
            credentialsListElement.innerHTML = '<p>保存されたデータはありません。</p>';
        } else {
            credentials.forEach((item, index) => {
                const credentialItem = document.createElement('div');
                credentialItem.className = 'credential-item';

                const urlP = document.createElement('p');
                urlP.textContent = `URL: ${item.url}`;

                const usernameP = document.createElement('p');
                usernameP.textContent = `ID: ${item.username}`;

                const passwordP = document.createElement('p');
                passwordP.textContent = `Password: ${item.password}`;

                const sendButton = document.createElement('button');
                sendButton.className = 'send-button';
                sendButton.textContent = '送信';
                sendButton.addEventListener('click', function () {
                    sendCredential(item, index); // 選択されたデータを送信
                });

                credentialItem.appendChild(urlP);
                credentialItem.appendChild(usernameP);
                credentialItem.appendChild(passwordP);
                credentialItem.appendChild(sendButton);

                credentialsListElement.appendChild(credentialItem);
            });
        }
    });

    // すべてクリアボタンのクリックイベント
    clearAllButton.addEventListener('click', function () {
        if (confirm('すべてのデータを削除しますか？')) {
            chrome.storage.local.set({ credentials: [] }, function () {
                console.log('すべてのデータを削除しました');
                window.location.reload(); // ページをリロードしてリストを更新
            });
        }
    });

    // テストメッセージボタンのクリックイベント
    testMessageButton.addEventListener('click', function () {
        sendTestMessage();
    });

    // フォームが存在する場合に送信時の処理を設定
    if (form) {
        form.addEventListener('submit', function (event) {
            event.preventDefault(); // デフォルト動作をキャンセル

            const inputText = document.getElementById('textInput').value;

            // WebSocketが開いているか確認してメッセージを送信
            if (socket && socket.readyState === WebSocket.OPEN) {
                socket.send(inputText); // 入力されたテキストをWebSocket経由で送信
                console.log("WebSocket経由で送信: " + inputText);

                // 7秒間待機してメッセージを受信
                waitForResponse(responseArea);
            } else {
                console.log("WebSocketが接続されていません");
                alert('WebSocketが接続されていません');
            }
        });
    }
});

// 7秒間待機してメッセージを受信する関数
function waitForResponse(responseArea) {
    let responseReceived = false;

    // WebSocketのメッセージ受信イベントを設定
    const messageHandler = function(event) {
        responseReceived = true;
        responseArea.innerHTML = `<p>受信メッセージ: ${event.data}</p>`;
        console.log('受信メッセージ: ' + event.data);

        // メッセージ受信後、リスナーをクリア
        socket.removeEventListener('message', messageHandler);
    };

    socket.addEventListener('message', messageHandler);

    // 7秒後に応答がなければタイムアウト処理
    setTimeout(function() {
        if (!responseReceived) {
            responseArea.innerHTML = `<p>応答なし（タイムアウト）</p>`;
            console.log('応答なし（タイムアウト）');
            socket.removeEventListener('message', messageHandler);
        }
    }, 7000); // 7秒待機
}

// C#プログラムを起動する関数
function startBluetoothBridge() {
    try {
        var hostName = "com.example.bluetooth_bridge"; // ホスト名を指定
        port = chrome.runtime.connectNative(hostName); // ネイティブホストに接続

        setTimeout(function () {
            console.log("3秒後にWebSocket接続を開始します...");
            appendMessage("3秒後にWebSocket接続を開始します...");
            connectWebSocket(); // 3秒後にWebSocket接続を開始
        }, 3000);

        port.onDisconnect.addListener(function () {
            console.log("C#プログラムが終了しました");
            appendMessage("C#プログラムが終了しました");
            port = null;
        });

        port.onMessage.addListener(function (message) {
            console.log('ネイティブアプリからのメッセージ:', message);
            if (message.type === 'response' && message.data === 'OK') {
                alert('ネイティブアプリからOKを受信しました。');
            }
        });

        console.log('C#プログラムを起動しました');
        appendMessage('C#プログラムを起動しました');
    } catch (e) {
        console.error("C#プログラムの起動に失敗しました: " + e.message);
        appendMessage("C#プログラムの起動に失敗しました");
    }
}

// WebSocket接続を確立する関数
function connectWebSocket() {
    try {
        socket = new WebSocket('ws://127.0.0.1:8181'); // WebSocketサーバーに接続

        socket.onopen = function () {
            console.log('WebSocket接続が確立されました');
            appendMessage('WebSocket接続が確立されました');
        };

        socket.onclose = function () {
            console.log('WebSocket接続が閉じられました');
            appendMessage('WebSocket接続が閉じられました');
        };

        socket.onerror = function (error) {
            console.error('WebSocketエラー: ' + error.message);
            appendMessage('WebSocketエラー: ' + error.message);
        };
    } catch (e) {
        console.error("WebSocket接続に失敗しました: " + e.message);
        appendMessage("WebSocket接続に失敗しました");
    }
}

// 資格情報を送信する関数
function sendCredential(item, index) {
    const chooseMessage = 'choose';
    if (socket && socket.readyState === WebSocket.OPEN) {
        socket.send(chooseMessage);
        console.log("WebSocket経由でchooseメッセージを送信しました: " + chooseMessage);
    } else {
        console.log("WebSocketが接続されていません");
        alert('WebSocketが接続されていません。');
        return;
    }

    const messageHandler = function(event) {
        if (event.data === 'OK') {
            console.log('OKを受信しました。データを送信します。');
            const dataString = `${item.url},${item.username},${item.password}`;
            socket.send(dataString);
            console.log("WebSocket経由でデータを送信しました: " + dataString);
            removeCredentialFromList(index);
            socket.removeEventListener('message', messageHandler);
        }
    };

    socket.addEventListener('message', messageHandler);

    setTimeout(function () {
        console.log("OKの応答がタイムアウトしました");
        alert('ネイティブアプリからの応答が7秒以内にありませんでした。送信に失敗しました。');
        socket.removeEventListener('message', messageHandler);
    }, 7000);
}

// リストから資格情報を削除して表示を更新する関数
function removeCredentialFromList(index) {
    chrome.storage.local.get({ credentials: [] }, function (result) {
        let credentials = result.credentials;
        credentials.splice(index, 1);
        chrome.storage.local.set({ credentials: credentials }, function () {
            console.log('データを更新しました');
            window.location.reload(); // ページをリロードしてリストを更新
        });
    });
}

// テストメッセージを送信する関数
function sendTestMessage() {
    const testMessage = "これはテストメッセージです";

    if (port) {
        port.postMessage({ type: 'testMessage', data: testMessage });
        console.log("ネイティブアプリにテストメッセージを送信しました");

        if (socket && socket.readyState === WebSocket.OPEN) {
            socket.send(testMessage);
            console.log("WebSocket経由でテストメッセージを送信しました: " + testMessage);
            alert('テストメッセージを送信しました');
        } else {
            console.log("WebSocketが接続されていません");
            alert('WebSocketが接続されていません');
        }
    } else {
        console.log("ネイティブアプリとのポートが開いていません");
        alert('ネイティブアプリとのポートが開いていません');
    }
}
