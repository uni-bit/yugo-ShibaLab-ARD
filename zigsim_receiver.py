import socket
import json
import argparse

def main():
    parser = argparse.ArgumentParser(description='ZIG SIM UDP JSON Receiver')
    parser.add_argument('--port', type=int, default=50000, help='Port to listen on (ZIG SIM default: 50000)')
    parser.add_argument('--ip', type=str, default='0.0.0.0', help='IP to listen on (default: 0.0.0.0)')
    args = parser.parse_args()

    # UDPソケットの作成
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((args.ip, args.port))

    print(f"[*] UDPポート {args.port} で受信待機中... ({args.ip})")
    print("[*] ZIG SIMアプリ側の設定:")
    print(f"    - IP Address: <このPCのローカルIPアドレス>")
    print(f"    - Port Number: {args.port}")
    print("    - Protocol: UDP")
    print("    - Format: JSON")
    print("[*] 終了するには Ctrl+C を押してください\n")

    try:
        while True:
            # データの受信 (バッファサイズ: 4096バイト)
            data, addr = sock.recvfrom(4096)
            
            try:
                # データをUTF-8文字列としてデコード
                decoded_data = data.decode('utf-8')
                
                # ZIG SIMのJSONフォーマットとしてパース (改行等を含めて綺麗に表示するため)
                json_data = json.loads(decoded_data)
                
                print(f"\n--- 受信元: {addr[0]}:{addr[1]} ---")
                print(json.dumps(json_data, indent=2, ensure_ascii=False))
                
                # 受信データをファイルにも保存する（データの消失防止用）
                with open('zigsim_received_data.jsonl', 'a', encoding='utf-8') as f:
                    f.write(json.dumps(json_data, ensure_ascii=False) + '\n')
                
            except json.JSONDecodeError:
                # JSONとしてパースできなかった場合は文字列データとして表示
                print(f"\n--- 受信元: {addr[0]}:{addr[1]} (Not JSON) ---")
                print(decoded_data)
                with open('zigsim_received_data_raw.log', 'a', encoding='utf-8') as f:
                    f.write(f"[{addr[0]}:{addr[1]}] {decoded_data}\n")
                
            except UnicodeDecodeError:
                # バイナリデータ（OSCフォーマットなどを選択している場合）
                print(f"\n--- 受信元: {addr[0]}:{addr[1]} (Binary Data/OSC) ---")
                print(data)
                with open('zigsim_received_data_bin.log', 'ab') as f:
                    f.write(data + b'\n')

    except KeyboardInterrupt:
        print("\n[*] 受信を終了しました。")
    finally:
        sock.close()

if __name__ == '__main__':
    main()
