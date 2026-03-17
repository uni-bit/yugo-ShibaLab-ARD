# 機能ガイド: スポットライト連動シェーダー

> **難易度**: ★★★☆☆  
> **再利用度**: ⭐ 高い — ライトで照らした部分だけ要素を表示させる演出全般に応用可能  
> **依存パッケージ**: URP (Universal Render Pipeline)

---

## この機能の概要

**スポットライトが当たっている位置のテキストだけが浮かび上がる** シェーダーです。

通常の Unity ライティングでは「テキスト」はライトの影響を受けません（UI や Unlit マテリアルを使うため）。このシェーダーは、スポットライトのワールド座標・方向・角度をシェーダーグローバル変数として受け取り、フラグメントシェーダー内で独自にライト計算を行うことで、**ライトの範囲内にあるテキストだけを可視化** します。

---

## 視覚的な効果

```
   暗い状態:                    ライトが当たった状態:
   ┌────────────────────┐      ┌────────────────────┐
   │                    │      │         ★★★        │
   │  (何も見えない)      │  →   │     SECRET CODE     │
   │                    │      │         ★★★        │
   └────────────────────┘      └────────────────────┘
                                  ↑ ライトの範囲だけ
                                    テキストが浮かぶ
```

---

## 関連ファイル

| ファイル | パス | 行数 |
|---------|------|------|
| `StageSpotlightText.shader` | `Assets/Shaders/` | ~97 |

---

## シェーダーグローバル変数

`PoseTestBootstrap.cs` が毎フレーム `Update()` で以下のグローバル変数を設定します。シェーダー側はこれを参照するだけなので、個別のマテリアル設定は不要です。

| 変数名 | 型 | 設定元 | 内容 |
|--------|-----|--------|------|
| `_StageSpotlightPosition` | Vector4 | `light.transform.position` | ライトのワールド位置 |
| `_StageSpotlightDirection` | Vector4 | `light.transform.forward` | ライトの向き (正規化) |
| `_StageSpotlightRange` | float | `light.range` | ライトの到達距離 |
| `_StageSpotlightCosOuter` | float | `cos(spotAngle / 2)` | スポット外角のコサイン |
| `_StageSpotlightCosInner` | float | `cos(innerAngle / 2)` | スポット内角のコサイン |
| `_StageSpotlightEnabled` | float | `1.0` or `0.0` | ライト有効フラグ |

### グローバル変数の設定コード (C# 側)

```csharp
// PoseTestBootstrap.Update() 内
Light light = ActiveSpotLight;
Shader.SetGlobalVector("_StageSpotlightPosition", light.transform.position);
Shader.SetGlobalVector("_StageSpotlightDirection", light.transform.forward);
Shader.SetGlobalFloat("_StageSpotlightRange", light.range);

float outerRadians = light.spotAngle * 0.5f * Mathf.Deg2Rad;
float innerRadians = light.innerSpotAngle * 0.5f * Mathf.Deg2Rad;
Shader.SetGlobalFloat("_StageSpotlightCosOuter", Mathf.Cos(outerRadians));
Shader.SetGlobalFloat("_StageSpotlightCosInner", Mathf.Cos(innerRadians));
Shader.SetGlobalFloat("_StageSpotlightEnabled", 1.0f);
```

---

## シェーダーコード全文解説

### Properties ブロック

```hlsl
Properties
{
    _MainTex    ("Texture", 2D)  = "white" {}
    _LitColor   ("Lit Color",   Color) = (1, 1, 1, 1)    // 照射時の色 (白)
    _HiddenColor("Hidden Color", Color) = (0, 0, 0, 0)    // 非照射時の色 (透明)
}
```

### Vertex Shader

```hlsl
v2f vert(appdata v)
{
    v2f o;
    o.pos      = UnityObjectToClipPos(v.vertex);
    o.uv       = TRANSFORM_TEX(v.uv, _MainTex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;  // ワールド座標を渡す
    return o;
}
```

### Fragment Shader (核心)

```hlsl
fixed4 frag(v2f i) : SV_Target
{
    // 1. ライトが無効ならテキストを非表示
    if (_StageSpotlightEnabled < 0.5)
        return _HiddenColor;

    // 2. ピクセルのワールド座標からライトへのベクトルを計算
    float3 toLight = _StageSpotlightPosition.xyz - i.worldPos;
    float distance = length(toLight);
    float3 dirToPixel = -normalize(toLight);  // ライトからピクセルへの方向

    // 3. 距離チェック: range を超えていたら非表示
    if (distance > _StageSpotlightRange)
        return _HiddenColor;

    // 4. 角度チェック: スポット角度の外なら非表示
    float cosAngle = dot(_StageSpotlightDirection.xyz, dirToPixel);
    if (cosAngle < _StageSpotlightCosOuter)
        return _HiddenColor;

    // 5. スポットライトファクターの計算
    //    内角〜外角の間でスムーズに減衰
    float spotFactor = saturate(
        (cosAngle - _StageSpotlightCosOuter)
        / (_StageSpotlightCosInner - _StageSpotlightCosOuter)
    );

    // 6. 距離による減衰
    float distFactor = saturate(1.0 - distance / _StageSpotlightRange);

    // 7. 最終的な照射量
    float lightFactor = spotFactor * distFactor;

    // 8. テクスチャの色を取得
    fixed4 texColor = tex2D(_MainTex, i.uv);

    // 9. Hidden ↔ Lit をブレンド
    fixed4 finalColor = lerp(_HiddenColor, _LitColor * texColor, lightFactor);
    return finalColor;
}
```

---

## SpotlightSensor との違い

| 項目 | SpotlightSensor (C#) | このシェーダー (GPU) |
|------|----------------------|---------------------|
| 実行場所 | CPU (MonoBehaviour) | GPU (Fragment Shader) |
| 判定単位 | オブジェクト 1 点 | ピクセルごと |
| 用途 | ゲームロジック用の判定 | ビジュアル表現 |
| 結果 | IsLit (bool) / Exposure01 (float) | ピクセルの色 |

**SpotlightSensor** は「当たっているかどうか」を判定するロジック用、**このシェーダー**は「照射部分だけ見えるようにする」ビジュアル用です。

---

## 自分のプロジェクトで実装するには

### 必要ファイル

1. **`StageSpotlightText.shader`** — シェーダー本体
2. C# 側のグローバル変数設定（`PoseTestBootstrap.cs` の該当部分、または独自スクリプトで）

### セットアップ手順

```
1. シェーダーファイルをプロジェクトにコピー

2. マテリアルを作成:
   - Shader: "Custom/StageSpotlightText" を選択
   - _LitColor: ライトが当たった時の色 (例: 白)
   - _HiddenColor: 暗い時の色 (例: 透明)

3. テキスト（TextMeshPro / 3DText / Mesh）にマテリアルを割り当て

4. C# スクリプトで毎フレームグローバル変数を更新:

   void Update()
   {
       Light spot = GetComponent<Light>();
       Shader.SetGlobalVector("_StageSpotlightPosition", spot.transform.position);
       Shader.SetGlobalVector("_StageSpotlightDirection", spot.transform.forward);
       Shader.SetGlobalFloat("_StageSpotlightRange", spot.range);
       Shader.SetGlobalFloat("_StageSpotlightCosOuter",
           Mathf.Cos(spot.spotAngle * 0.5f * Mathf.Deg2Rad));
       Shader.SetGlobalFloat("_StageSpotlightCosInner",
           Mathf.Cos(spot.innerSpotAngle * 0.5f * Mathf.Deg2Rad));
       Shader.SetGlobalFloat("_StageSpotlightEnabled", 1.0f);
   }
```

### URP 以外 (Built-in RP) で使う場合

このシェーダーは URP 固有の機能は使っていないため、**Built-in RP でもそのまま動作** します。`Shader "Custom/StageSpotlightText"` と宣言しているだけで、URP のシェーダーライブラリには依存していません。

### カスタマイズ例

| 用途 | やり方 |
|------|--------|
| 色を変える | `_LitColor` を変更 |
| 完全に消す | `_HiddenColor = (0,0,0,0)` + Transparent Queue |
| ぼんやり残す | `_HiddenColor = (0.1, 0.1, 0.1, 0.3)` |
| 複数ライト対応 | グローバル変数を配列化し、for ループで max を取る |
| エッジを柔らかく | `spotFactor` に `smoothstep` を適用 |
| パルス演出 | `lightFactor` に `sin(_Time.y * frequency)` を掛ける |

---

## よくあるトラブルと対処法

| 症状 | 原因 | 対処 |
|------|------|------|
| テキストが常に非表示 | グローバル変数が設定されていない | C# 側の `Shader.SetGlobal...` を確認 |
| テキストが常に表示 | `_StageSpotlightEnabled = 0` になっていない | ライト無効時は `0.0` を設定 |
| 照射範囲がずれる | `spotAngle` が度ではなくラジアンで渡されている | `Deg2Rad` 変換を確認 |
| 裏面が表示されない | Cull Back が有効 | `Cull Off` に変更する |
