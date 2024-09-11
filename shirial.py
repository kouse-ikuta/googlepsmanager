import serial

# COMポートとボーレートを指定
ser = serial.Serial('COM15', 9600, timeout=1)

print("Waiting for data...")

# データの受信と表示を繰り返す
try:
    while True:
        # 1行分のデータを読み込む (改行で区切られたデータ)
        if ser.in_waiting > 0:
            received_data = ser.readline().decode('utf-8').strip()
            if received_data:
                print(f"Received: {received_data}")

except KeyboardInterrupt:
    # ユーザーがCtrl+Cを押して終了した場合
    print("Exiting program...")

finally:
    # 通信終了時にシリアルポートを閉じる
    ser.close()

