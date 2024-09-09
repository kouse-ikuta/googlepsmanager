document.getElementById('connectBtn').addEventListener('click', function() {
    navigator.bluetooth.requestDevice({
        acceptAllDevices: true,
        optionalServices: ['battery_service']  // 対応するサービスに合わせて変更
    })
    .then(device => {
        console.log('Connecting to GATT Server...');
        return device.gatt.connect();
    })
    .then(server => {
        console.log('Connected to GATT Server');
        document.getElementById('status').textContent = 'Connected';
    })
    .catch(error => {
        console.error('Connection failed: ', error);
        document.getElementById('status').textContent = 'Connection failed';
    });
});
