# 機能ガイド: スポットライト照射判定 (SpotlightSensor)

> **難易度**: ★★☆☆☆  
> **再利用度**: ⭐ 高い — ライトで何かを「照らして反応させる」ギミック全般に応用可能  
> **依存パッケージ**: なし（Unity 標準 API のみ）

---

## この機能の概要

**スポットライトがオブジェクトを照らしているかどうか** を毎フレーム計算するセンサーコンポーネントです。角度と距離から **0〜1 の露出量 (`Exposure01`)** を算出し、閾値を超えたら `IsLit = true` を返します。

このプロジェクトのすべてのステージギミック（Stage 1〜4）が、この `SpotlightSensor` を共通基盤として使っています。

---

## 関連ファイル

| ファイル | パス | 行数 |
|---------|------|------|
| `SpotlightSensor.cs` | `Assets/Scripts/Stages/` | ~162 |

---

## 照射量の計算式

```
           Light
           🔦 ────────────→ forward
           │╲
           │  ╲  halfAngle
           │    ╲
           │      ╲
    distance│        ● Target (samplePoint)
           │      ╱
           │    ╱
           │  ╱  angleToTarget
           │╱

Exposure01 = angularFalloff × distanceFalloff

  angularFalloff  = 1 − (angleToTarget / halfAngle)
  distanceFalloff = 1 − (distance / range)

  IsLit = (Exposure01 ≥ activationThreshold)
```

### 角度フォールオフ

スポットライトの中心に近いほど値が高く、端に行くほど 0 に近づきます。`spotAngle` の半角を超えると即座に 0 になります。

### 距離フォールオフ

近いほど値が高く、`range` に達すると 0 になります。

### 最終露出量

角度と距離を **掛け算** して 0〜1 の値にします。中心かつ近い = 1.0、端かつ遠い = 0.0 です。

---

## Inspector 設定

| フィールド | 型 | デフォルト | 説明 |
|-----------|-----|----------|------|
| `bootstrap` | PoseTestBootstrap | (自動解決) | ライトの取得元。`FindFirstObjectByType` で自動検索 |
| `sourceLight` | Light | (自動解決) | 判定に使うスポットライト |
| `samplePoint` | Transform | 自分自身 | 照射判定する座標の基準 |
| `sampleRenderer` | Renderer | (自動検索) | samplePoint が null のときの代替 (bounds.center) |
| `sampleCollider` | Collider | (自動検索) | sampleRenderer が null のときの代替 |
| `activationThreshold` | float | `0.2` | IsLit になる最低 Exposure01 |
| `requireLineOfSight` | bool | `false` | true にすると Raycast で遮蔽判定を追加 |
| `occlusionMask` | LayerMask | 全レイヤー | Line of Sight 判定で使うレイヤー |

---

## コード全文の解説

```csharp
public class SpotlightSensor : MonoBehaviour
{
    // === 公開プロパティ ===
    public bool  IsLit      { get; private set; }  // 閾値以上ならtrue
    public float Exposure01 { get; private set; }  // 0〜1の露出量

    // === 毎フレーム実行 ===
    private void Update()
    {
        RefreshState();  // 毎フレーム再計算
    }

    public void RefreshState()
    {
        ResolveLight();                  // ライトが未設定なら自動解決
        Exposure01 = EvaluateExposure(); // 露出量を計算
        IsLit = Exposure01 >= activationThreshold;
    }

    // === ライトの自動解決 ===
    private void ResolveLight()
    {
        if (sourceLight != null) return;

        // PoseTestBootstrap を探して ActiveSpotLight を取得
        if (bootstrap == null)
            bootstrap = FindFirstObjectByType<PoseTestBootstrap>();
        if (bootstrap != null)
            sourceLight = bootstrap.ActiveSpotLight;
    }

    // === 露出量の計算 ===
    private float EvaluateExposureAtPointInternal(Vector3 sampleWorldPosition)
    {
        // 条件チェック
        if (sourceLight == null) return 0f;
        if (!sourceLight.enabled) return 0f;
        if (sourceLight.type != LightType.Spot) return 0f;

        // 距離チェック
        Vector3 lightToSample = sampleWorldPosition - sourceLight.transform.position;
        float distance = lightToSample.magnitude;
        if (distance > sourceLight.range) return 0f;

        // 角度チェック
        Vector3 direction = lightToSample / distance;
        float angleToTarget = Vector3.Angle(sourceLight.transform.forward, direction);
        float halfAngle = sourceLight.spotAngle * 0.5f;
        if (angleToTarget > halfAngle) return 0f;

        // Line of Sight (オプション)
        if (requireLineOfSight)
        {
            // 自分以外のコライダーに先に当たったら遮蔽されている
            RaycastHit[] hits = Physics.RaycastAll(...);
            // ... (最初に自分以外に当たったら return 0f)
        }

        // フォールオフ計算
        float angularFalloff = 1f - (angleToTarget / halfAngle);
        float distanceFalloff = 1f - (distance / sourceLight.range);
        return angularFalloff * distanceFalloff;
    }
}
```

---

## 使われ方の例

### Stage 1: 順番照射パズル

```csharp
// StageLightOrderedPuzzle の中で
SpotlightSensor sensor = currentTarget.GetComponent<SpotlightSensor>();
if (sensor.IsLit)
{
    focusTimer += Time.deltaTime;
    if (focusTimer >= requiredFocusSeconds)
        AdvanceProgress();  // 次のターゲットへ
}
```

### Stage 2: コードロック

```csharp
// StageLightCodeDialColumn の中で
SpotlightSensor upSensor = upButton.GetComponent<SpotlightSensor>();
if (upSensor.IsLit && canOperate)
{
    currentDigit = (currentDigit + 1) % 10;  // 数字を +1
}
```

### Stage 2: テキスト発光

```csharp
// StageSymbolNumberRevealTarget の中で
float exposure = sensor.Exposure01;  // 0〜1
// exposure に応じて文字の明るさを変化
```

---

## 自分のプロジェクトで実装するには

### 最小構成（1 ファイル）

`SpotlightSensor.cs` をそのままコピーするだけで使えます。

### Bootstrap なしで使う場合

`PoseTestBootstrap` に依存しないようにするには、Inspector で `sourceLight` に直接スポットライトを割り当てます。または起動時に:

```csharp
sensor.SetLightSource(mySpotLight);
```

### セットアップ手順

```
1. 「照らされたいオブジェクト」に SpotlightSensor を付ける
2. sourceLight にスポットライトを割り当てる
   (PoseTestBootstrap がシーンにあれば自動解決される)
3. activationThreshold を調整 (デフォルト 0.2)
4. 必要なら requireLineOfSight = true で壁越し判定を防止

5. 他のスクリプトから判定結果を使う:
   if (sensor.IsLit)         → ライトが当たっている
   sensor.Exposure01         → 0〜1 の照射量 (グラデーション的な反応に)
```

### カスタマイズ例

| 用途 | やり方 |
|------|--------|
| もっと広い範囲で反応 | `activationThreshold` を下げる (0.05 など) |
| 中心だけで反応 | `activationThreshold` を上げる (0.7 など) |
| 壁越しで反応させない | `requireLineOfSight = true` |
| ポイントライトで使う | `sourceLight.type != LightType.Spot` のチェックを外す改修が必要 |
| 複数ライトで判定 | SpotlightSensor を複数付けるか、max(exposure) を取る拡張が必要 |
