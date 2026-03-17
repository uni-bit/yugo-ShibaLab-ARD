# 機能ガイド: スポットライト照射判定 (SpotlightSensor)

> **難易度**: ★★☆☆☆  
> **再利用度**: ⭐ 高い — ライトで何かを「照らして反応させる」ギミック全般に応用可能  
> **依存パッケージ**: なし（Unity 標準 API のみ）

---

## この機能の概要

**スポットライトがオブジェクトを照らしているかどうか** を毎フレーム計算するセンサーコンポーネントです。角度と距離から **0〜1 の露出量 (`Exposure01`)** を算出し、閾値を超えたら `IsLit = true` を返します。

このプロジェクトのすべてのステージギミック（Stage 1〜4）が、この `SpotlightSensor` を共通基盤として使っています。

> **露出量 (Exposure) とは**: ライトがどのくらい当たっているかを 0（まったく当たっていない）〜 1（真正面で最大に当たっている）で表した値。

> **閾値 (Threshold) とは**: 「これ以上なら ON」という境目の値。デフォルト 0.2 = Exposure01 が 0.2 以上なら「照らされている」と判定。

---

## 関連ファイル

| ファイル | パス | 行数 |
|---------|------|------|
| `SpotlightSensor.cs` | `Assets/Scripts/Stages/` | ~162 |

---

## 照射量の計算式

```
           Light（スポットライト）
           🔦 ────────────→ forward（ライトが向いている方向）
           │╲
           │  ╲  halfAngle（スポット角度の半分）
           │    ╲
           │      ╲
    distance│        ● Target (samplePoint = 判定したい点)
    (距離)  │      ╱
           │    ╱
           │  ╱  angleToTarget（ライト中心→ターゲットまでの角度）
           │╱

計算式:
  angularFalloff  = 1 − (angleToTarget / halfAngle)
                   → ライト中心に近いほど 1、端に近いほど 0

  distanceFalloff = 1 − (distance / range)
                   → 近いほど 1、range に達すると 0

  Exposure01 = angularFalloff × distanceFalloff
              → 中心かつ近い = 1.0、端かつ遠い = 0.0

  IsLit = (Exposure01 ≥ activationThreshold)
         → 閾値以上なら「照らされている」
```

> **`falloff`（フォールオフ）とは**: 値がだんだん減衰していくこと。ライトは中心から離れるほど弱くなる（角度フォールオフ）し、遠ざかるほど弱くなる（距離フォールオフ）。

---

## Inspector 設定

| フィールド | 型 | デフォルト | 何のために設定するか |
|-----------|-----|----------|-------------------|
| `bootstrap` | PoseTestBootstrap | (自動解決) | ライトの取得元。設定しなくても自動で見つける |
| `sourceLight` | Light | (自動解決) | 判定に使うスポットライト。通常は自動で設定される |
| `samplePoint` | Transform | 自分自身 | 「この場所が照らされているか」を判定する座標 |
| `activationThreshold` | float | `0.2` | Exposure01 がこの値以上なら `IsLit = true` |
| `requireLineOfSight` | bool | `false` | true にすると壁越しに照射が届かなくなる |

> **`requireLineOfSight`（ライン・オブ・サイト = 視線）とは**: ライトからターゲットまでの間に遮るもの（壁など）がないか Raycast で確認する機能。false だと壁の向こうでも照射判定される。

> **`Raycast`（レイキャスト）とは**: Unity の機能で、指定した方向に見えない「光線（Ray）」を飛ばして、何かにぶつかるか確認する仕組み。FPS ゲームの弾丸判定にも使われる。

---

## コードの解説

```csharp
public class SpotlightSensor : MonoBehaviour
{
    // === 公開プロパティ（他のスクリプトから読める値）===
    public bool  IsLit      { get; private set; }  // 閾値以上なら true
    public float Exposure01 { get; private set; }  // 0〜1 の露出量
```

> **解説**:
> - **`{ get; private set; }`**: 他のスクリプトからは「読める」が「書き換えられない」プロパティ。データの安全性を守る書き方
> - 他のスクリプト（パズルなど）は `sensor.IsLit` や `sensor.Exposure01` で値を読み取る

```csharp
    // === 毎フレーム実行 ===
    private void Update()
    {
        RefreshState();  // 毎フレーム露出量を再計算
    }

    public void RefreshState()
    {
        ResolveLight();                  // ライトが未設定なら自動で探す
        Exposure01 = EvaluateExposure(); // 露出量を計算
        IsLit = Exposure01 >= activationThreshold;  // 閾値判定
    }
```

> **解説**:
> - **`Update()`**: Unity が毎フレーム自動で呼ぶ関数（1 秒間に 60 回くらい）
> - **`ResolveLight()`**: `sourceLight` が null（未設定）の場合、シーン内から PoseTestBootstrap を探してスポットライトを自動で見つける
> - `IsLit` は `Exposure01 >= 0.2`（デフォルト値）のとき true になる

```csharp
    // === 露出量の計算（核心部分）===
    private float EvaluateExposureAtPointInternal(Vector3 sampleWorldPosition)
    {
        // 前提条件チェック
        if (sourceLight == null) return 0f;        // ライトがない
        if (!sourceLight.enabled) return 0f;       // ライトが OFF
        if (sourceLight.type != LightType.Spot) return 0f; // スポットライト以外は対象外

        // 距離チェック
        Vector3 lightToSample = sampleWorldPosition - sourceLight.transform.position;
        float distance = lightToSample.magnitude;
        if (distance > sourceLight.range) return 0f;  // range 外 → 0

        // 角度チェック
        Vector3 direction = lightToSample / distance;  // 正規化（長さ1に）
        float angleToTarget = Vector3.Angle(sourceLight.transform.forward, direction);
        float halfAngle = sourceLight.spotAngle * 0.5f;
        if (angleToTarget > halfAngle) return 0f;  // スポット角度の外 → 0

        // Line of Sight チェック（オプション）
        if (requireLineOfSight)
        {
            // Raycast で途中に壁があるか確認
            // 壁に当たったら遮蔽されている → return 0f
        }

        // フォールオフ計算
        float angularFalloff = 1f - (angleToTarget / halfAngle);
        float distanceFalloff = 1f - (distance / sourceLight.range);
        return angularFalloff * distanceFalloff;  // 掛け算で最終値
    }
}
```

> **解説**:
> - **`magnitude`（マグニチュード）**: ベクトルの「長さ」。ここではライトからターゲットまでの距離
> - **`Vector3.Angle(a, b)`**: 2 つの方向ベクトルの間の角度を度数法（0〜180°）で返す
> - **`sourceLight.spotAngle`**: スポットライトの全角度（例: 60°）。半角は 30°
> - **`sourceLight.range`**: ライトが届く最大距離（メートル）
> - 最後の掛け算がポイント: 角度が中心で距離が近い → 1×1=1（最大）、どちらかが端 → 掛け算で小さくなる

---

## 使われ方の例

### Stage 1: 順番照射パズル

```csharp
SpotlightSensor sensor = currentTarget.GetComponent<SpotlightSensor>();
if (sensor.IsLit)
{
    focusTimer += Time.deltaTime;       // 照射している時間を積算
    if (focusTimer >= requiredFocusSeconds)
        AdvanceProgress();              // 一定秒数照射 → 次のターゲットへ
}
```

> **解説**:
> - **`GetComponent<SpotlightSensor>()`**: そのオブジェクトに付いている SpotlightSensor を取得する Unity の関数
> - **`Time.deltaTime`**: 前のフレームからの経過秒数（60fps なら約 0.016 秒）。これを足し続けることで「何秒間照射したか」を計測

### Stage 2: コードロック

```csharp
SpotlightSensor upSensor = upButton.GetComponent<SpotlightSensor>();
if (upSensor.IsLit && canOperate)
{
    currentDigit = (currentDigit + 1) % 10;  // 数字を +1（9 の次は 0）
}
```

> **解説**:
> - **`% 10`（剰余演算子）**: 10 で割った余り。0→1→2→...→9→0 とループさせるテクニック

---

## 自分のプロジェクトで実装するには

### 最小構成（1 ファイル）

`SpotlightSensor.cs` をそのままコピーするだけで使えます。

### セットアップ手順

```
1. 「照らされたいオブジェクト」に SpotlightSensor を付ける
2. sourceLight にスポットライトを割り当てる
   (PoseTestBootstrap がシーンにあれば自動解決される)
3. activationThreshold を調整（デフォルト 0.2）
4. 必要なら requireLineOfSight = true で壁越し判定を防止

5. 他のスクリプトから判定結果を使う:
   if (sensor.IsLit)         → ライトが当たっている
   sensor.Exposure01         → 0〜1 の照射量
```

### カスタマイズ例

| 用途 | やり方 |
|------|--------|
| もっと広い範囲で反応 | `activationThreshold` を下げる（0.05 など） |
| 中心だけで反応 | `activationThreshold` を上げる（0.7 など） |
| 壁越しで反応させない | `requireLineOfSight = true` |
| ポイントライトで使う | `sourceLight.type != LightType.Spot` のチェックを外す改修が必要 |
