// ログインフォームのIDとパスワードを取得する関数
function getLoginInfo() {
    let usernameInput = document.querySelector('input[type="text"], input[type="email"]');
    let passwordInput = document.querySelector('input[type="password"]');

    if (usernameInput && passwordInput) {
        let username = usernameInput.value;
        let password = passwordInput.value;

        if (username && password) {
            // ローカルストレージに保存
            chrome.storage.local.set({
                "username": username,
                "password": password
            }, function () {
                console.log("Username and password saved.");
            });
        }
    }
}

// フォーム送信時にIDとパスワードを自動取得する
document.addEventListener('submit', getLoginInfo);
