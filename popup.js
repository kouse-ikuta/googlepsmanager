document.addEventListener('DOMContentLoaded', function () {
    // 保存されたIDとパスワードを取得して表示
    chrome.storage.local.get(["username", "password"], function (data) {
        document.getElementById('username').textContent = data.username || 'No data';
        document.getElementById('password').textContent = data.password || 'No data';
    });
});
