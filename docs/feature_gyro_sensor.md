# 機能ガイド: スマホジャイロセンサー入力

> **難易度**: ★★★☆☆  
> **再利用度**: ⭐ 高い — スマホの姿勢で何かを操作するプロジェクト全般に応用可能  
> **依存パッケージ**: なし（Unity 標準 API のみ）

---

## この機能の概要

スマートフォン（iPhone / Android）の **ジャイロセンサー（姿勢センサー）** から **クォータニオン（回転情報）** を受け取り、Unity 側のオブジェクトの向きに反映する仕組みです。

スマホアプリ **「ZIG SIM」** を使い、**UDP/OSC プロトコル** でクォータニオンデータを PC に送信します。Unity 側は別スレッドで UDP パケットを受信し、座標変換 → キャリブレーション → 回転適用 の 3 段階でスポットライトの向きに変換します。

---

## データフロー図

```
┌───────────────┐   UDP/OSC     ┌──────────────────────┐
│  📱 スマホ      │ ──────────▶ │ UdpQuaternionReceiver │
│  ZIG SIM アプリ  │  ポート 8000  │ (別スレッドで受信)      │
│  ジャイロ送信    │              │ OSC/JSON/テキスト/     │
└───────────────┘              │ バイナリを自動判別      │
                               └──────────┬───────────┘
                                          │ 生クォータニオン (x,y,z,w)
                                          ▼
                              ┌──────────────────────────┐
                              │ QuaternionCoordinateConverter │
                              │ iPhone CoreMotion / Android   │
                              │ RotationVector → Unity 左手系  │
                              └──────────┬───────────────────┘
                                          │ Unity 座標系クォータニオン
                                          ▼
                              ┌──────────────────────────┐
                              │    PoseRotationDriver      │
                              │ 初回自動キャリブレーション    │
                              │ 相対回転を計算してターゲットに │
                              │ localRotation として適用     │
                              └──────────┬───────────────┘
                                          │
                                          ▼
                               🔦 回転するオブジェクト
                            (このプロジェクトではスポットライト)
```

---

## 関連ファイル

| ファイル | パス | 行数 |
|---------|------|------|
| `UdpQuaternionReceiver.cs` | `Assets/Scripts/Pose/` | ~1637 |
| `QuaternionCoordinateConverter.cs` | `Assets/Scripts/Pose/` | ~216 |
| `QuaternionCalibrationUtility.cs` | `Assets/Scripts/Pose/` | ~10 |
| `PoseRotationDriver.cs` | `Assets/Scripts/Pose/` | ~199 |
| `PoseCalibrationCoordinator.cs` | `Assets/Scripts/Pose/` | ~130 |

---

## 各ファイルの詳細解説

### 1. UdpQuaternionReceiver.cs — UDP 受信の中核

**役割**: 別スレッドで UDP ソケットをリッスンし、スマホから届くパケットからクォータニオンとタッチ入力を抽出する。

#### Inspector 設定

| フィールド | 型 | デフォルト | 説明 |
|-----------|-----|----------|------|
| `listenPort` | int | `8000` | UDP 待ち受けポート。ZIG SIM 側と合わせる |
| `coordinatePreset` | enum | `IPhoneCoreMotion` | `IPhoneCoreMotion` か `AndroidRotationVector` |
| `convertRightHandedToLeftHanded` | bool | `true` | 右手系→左手系変換を行うか |
| `screenFaceDown` | bool | `true` | スマホ画面下向き使用時の左右反転補正 |
| `stabilizeQuaternionHemisphere` | bool | `true` | クォータニオンの符号ジャンプ防止 |

#### 受信スレッド処理 (`ReceiveLoop`)

```csharp
// 簡略化した疑似コード
while (isRunning)
{
    byte[] packet = udpClient.Receive(ref remoteEndPoint);

    // 1. タッチパケットを検出 → リセンター要求
    TryRequestRecenterFromTouchMessages(oscMessages);

    // 2. クォータニオンを抽出 (4種のフォーマットを順に試行)
    //    OSC バンドル/メッセージ → JSON → テキスト (CSV) → バイナリ (16byte)
    QuaternionPacketParseResult result = TryParseQuaternionPacket(packet);

    // 3. 正規化＋半球安定化
    Quaternion raw = StabilizeRawQuaternion(result.Quaternion);

    // 4. 座標系変換
    Quaternion converted = QuaternionCoordinateConverter.ConvertToUnity(
        raw, coordinatePreset, eulerOffset, convertHandedness, screenFaceDown);

    // 5. メインスレッドへ渡す (lock で保護)
    pendingRotation = converted;
    hasPendingRotation = true;
}
```

#### パケットフォーマット判定の優先順位

```
1. OSC バンドル (#bundle ヘッダ) → 再帰的にメッセージを展開
2. OSC メッセージ (/ から始まる) → typetag + float arguments
3. JSON ("quaternion": {x, y, z, w}) → 正規表現で抽出
4. テキスト CSV (4つの float) → 正規表現で抽出
5. バイナリ 16byte → little/big endian 両方試行
```

#### メインスレッドとの通信

スレッド安全のため `lock (syncRoot)` でデータを保護し、メインスレッドから以下のメソッドで消費します:

```csharp
// メインスレッドから呼ぶ
bool hasNew = receiver.ConsumeLatestRotation(out Quaternion rotation);
// → true なら新しい回転データが来ている

bool hasRecenter = receiver.ConsumePendingRecenterRequest();
// → true ならタッチリセンター要求

bool hasTouch = receiver.ConsumePendingTouchPosition(out Vector2 position);
// → true ならタッチ位置が来ている
```

#### タッチ入力によるリセンター

ZIG SIM のタッチデータを検出すると `RecenterRequestCount` をインクリメントし、`PoseCalibrationCoordinator` 側でキャリブレーションリセットを実行します。クールダウン (`touchRecenterCooldownSeconds`) つき。

---

### 2. QuaternionCoordinateConverter.cs — 座標系変換

**役割**: iPhone CoreMotion / Android RotationVector の座標系を Unity の左手系に変換する。

#### iPhone CoreMotion の変換ロジック

```
iPhone のデバイス軸:
  +X = 画面右
  +Y = 画面上
  +Z = 画面から外向き (手前)

変換のアプローチ:
  1. クォータニオンで「デバイスの右方向」「上方向」「画面外方向」を回転
  2. LookRotation で Unity 回転に変換:
     - forward = デバイスの上方向 (スマホの先端が向いている方向)
     - up = デバイスの画面外方向の反転
```

```csharp
// screenFaceDown = false の場合
return Quaternion.LookRotation(
    deviceTop.normalized,        // forward = スマホの先端方向
    -deviceScreenOut.normalized  // up = 画面裏方向
);

// screenFaceDown = true の場合
return Quaternion.LookRotation(
    deviceTop.normalized,
    deviceScreenOut.normalized   // 反転しない (既に裏向き)
);
```

#### Android RotationVector の変換

```
Android 座標系: ENU 右手系 (X=East, Y=North, Z=Up)
Unity 座標系: 左手系 (X=right, Y=up, Z=forward)
変換: Z 軸の符号を反転 → w の符号も反転
```

```csharp
// screenFaceDown = false
new Quaternion(q.x, q.y, -q.z, -q.w);

// screenFaceDown = true (X も反転して左右を補正)
new Quaternion(-q.x, q.y, -q.z, -q.w);
```

#### 半球安定化

クォータニオンには `q` と `-q` が同じ回転を表す二重性があります。連続フレームで符号がジャンプするとスムーズな補間ができないため、前フレームとの内積で同じ半球に揃えます:

```csharp
float dot = Quaternion.Dot(normalized, lastNormalized);
if (dot < 0f)
{
    normalized = new Quaternion(-q.x, -q.y, -q.z, -q.w);
}
```

---

### 3. PoseRotationDriver.cs — 回転適用

**役割**: 受信クォータニオンをキャリブレーション基準の **相対回転** に変換し、ターゲットオブジェクトの `localRotation` に適用する。

#### 相対回転の計算

```csharp
// キャリブレーション時のセンサー回転を保存
referenceSensorRotation = firstPacketRotation;

// 毎フレーム: 基準からの相対回転を計算
// CalculateRelativeRotation = Inverse(reference) * current
Quaternion relativeRotation = Inverse(reference) * nextRotation;

// モデルオフセット適用 + 初期回転に合成
targetLocalRotation = initialLocalRotation * relativeRotation * Euler(modelEulerOffset);
```

#### 自動キャリブレーション

`autoCalibrateOnFirstPacket = true` のとき、最初のパケット受信時に自動でキャリブレーション基準を設定します。これにより、ゲーム開始時のスマホの向きが「正面」扱いになります。

#### Tip Light の追従

スポットライト (`tipLight`) が Rotation Pivot の子オブジェクトの場合は `localPosition = forward * offset` で追従。別の親にある場合はワールド座標で追従させます。

---

### 4. PoseCalibrationCoordinator.cs — キャリブレーション調停

**役割**: キーボード入力 / スマホタッチ入力によるキャリブレーションリセットを一括実行する。

```
C キー押下 or スマホタッチ検出
  ↓
ResetAllCalibration()
  ├── driver.ResetCalibration()     ← 回転を初期化、次パケットで再キャリブレーション
  ├── visualizer.ResetReference()   ← ポインタ表示をリセット
  └── bootstrap.ResetViewerRigPose() ← Viewer Origin と Rotation Pivot を初期位置に
```

---

## 自分のプロジェクトで実装するには

### 最小構成（3 ファイル）

1. **`UdpQuaternionReceiver.cs`** をそのままコピー
2. **`QuaternionCoordinateConverter.cs`** をそのままコピー
3. **`QuaternionCalibrationUtility.cs`** をそのままコピー

### セットアップ手順

```
1. 空の GameObject を作り、UdpQuaternionReceiver を付ける
2. Inspector で listenPort を ZIG SIM と合わせる (デフォルト: 8000)
3. coordinatePreset を iPhone / Android に合わせて設定
4. screenFaceDown をスマホの持ち方に合わせて設定

5. 回転させたいオブジェクトの Update で:
   Quaternion rotation;
   if (receiver.ConsumeLatestRotation(out rotation))
   {
       transform.localRotation = rotation;
   }
```

### ZIG SIM 側の設定

```
- IP Address: PC の ローカル IP アドレス
- Port Number: 8000 (Unity 側と一致させる)
- Protocol: UDP
- Format: JSON または OSC
- Sensor: Quaternion にチェック
```

### PoseRotationDriver を使う場合（推奨）

キャリブレーション付きで使いたい場合は `PoseRotationDriver` も追加します。

```csharp
// Configure で初期設定
driver.Configure(
    receiver,              // UDP 受信コンポーネント
    rotationTarget,        // 回転させる Transform
    tipLight,              // 追従させるライト (null 可)
    tipLightForwardOffset  // ライトの前方オフセット
);

// C キー等でキャリブレーションリセット
driver.ResetCalibration();
```

### デバッグ用 Python スクリプト

`zigsim_receiver.py` を使うと、ZIG SIM からデータが正しく届いているか Unity なしで確認できます。

```bash
python zigsim_receiver.py --port 50000
```

---

## よくあるトラブルと対処法

| 症状 | 原因 | 対処 |
|------|------|------|
| 回転が反映されない | ポートがファイアウォールでブロック | UDP 8000 番を許可 |
| 左右が逆 | `screenFaceDown` の設定ミス | スマホの画面向きに合わせて切り替え |
| 回転がカクカク | パケットロスまたは低 FPS | WiFi 接続の安定化 / 有線接続 |
| 回転方向がおかしい | `coordinatePreset` が不一致 | iPhone なら `IPhoneCoreMotion`、Android なら `AndroidRotationVector` |
| 急に 180° ジャンプ | 半球安定化が無効 | `stabilizeQuaternionHemisphere = true` |
