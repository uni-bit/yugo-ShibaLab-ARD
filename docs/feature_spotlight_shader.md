# 機能ガイド: スポットライト連動シェーダー

> **難易度**: ★★★☆☆  
> **再利用度**: ⭐ 高い — ライトで照らした部分だけ要素を表示させる演出全般に応用可能  
> **依存パッケージ**: URP (Universal Render Pipeline)。ただし投影計算自体は Built-in RP でも動作

---

## この機能の概要

**スポットライトが当たっている位置のテキストだけが浮かび上がる** シェーダーです。

> **シェーダーとは**: GPU（グラフィックカード）で動く小さなプログラム。「各ピクセルをどんな色で描くか」を決める。C# とは別の言語（HLSL）で書かれる。

通常の Unity ライティングでは「テキスト」はライトの影響を受けません（UI や Unlit マテリアルを使うため）。このシェーダーは、スポットライトの位置・方向・角度をシェーダーグローバル変数として受け取り、**フラグメントシェーダー内で独自にライト計算を行う** ことで、ライトの範囲内にあるテキストだけを可視化します。

> **フラグメントシェーダーとは**: 画面上の各ピクセル 1 つ 1 つに対して「何色にするか」を決めるプログラム。1 フレームで数百万回実行される。
>
> **Unlit マテリアルとは**: ライトの影響を受けないマテリアル。UI テキストなどはこれを使うため、普通はスポットライトを当てても明るくならない。

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
                                    テキストが浮かび上がる
```

---

## 関連ファイル

| ファイル | パス | 行数 |
|---------|------|------|
| `StageSpotlightText.shader` | `Assets/Shaders/` | ~97 |

---

## シェーダーグローバル変数

`PoseTestBootstrap.cs` が毎フレーム `Update()` で以下の値を設定します。シェーダー側はこれを参照するだけ。

| 変数名 | 型 | 何を入れるか |
|--------|-----|------------|
| `_StageSpotlightPosition` | Vector4 | ライトのワールド位置（x, y, z） |
| `_StageSpotlightDirection` | Vector4 | ライトの向き（正規化ベクトル） |
| `_StageSpotlightRange` | float | ライトの到達距離（メートル） |
| `_StageSpotlightCosOuter` | float | スポット外角のコサイン値 |
| `_StageSpotlightCosInner` | float | スポット内角のコサイン値 |
| `_StageSpotlightEnabled` | float | ライト有効フラグ（1.0 = ON, 0.0 = OFF） |

> **コサイン値を渡す理由**: シェーダー内で「角度が範囲内か」を判定するとき、`cos()` の比較のほうが `acos()` で角度に戻すより計算が軽い（GPU は毎ピクセル処理するので効率が重要）。

### グローバル変数の設定コード（C# 側）

```csharp
// PoseTestBootstrap.Update() 内で毎フレーム実行
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

> **解説**:
> - **`Shader.SetGlobalVector()`**: C# から GPU のシェーダー全体に値を渡す関数。個別のマテリアルに設定する必要がない
> - **`Mathf.Deg2Rad`**: 「度」を「ラジアン」に変換する定数（× π/180）。`Mathf.Cos()` はラジアンを受け取るため
> - **`light.spotAngle * 0.5f`**: Unity の `spotAngle` は全角度（例: 60°）なので、半角（30°）にする

---

## シェーダーコード全文解説

### Properties ブロック

```hlsl
Properties
{
    _MainTex    ("Texture", 2D)  = "white" {}
    _LitColor   ("Lit Color",   Color) = (1, 1, 1, 1)    // 照射時の色（白）
    _HiddenColor("Hidden Color", Color) = (0, 0, 0, 0)    // 非照射時の色（透明）
}
```

> **解説**:
> - **`Properties`**: Inspector 上で設定できるシェーダーパラメータ
> - **`_MainTex`**: テクスチャ（画像）。テキストのフォント画像がここに入る
> - **`_LitColor`**: ライトが当たっている部分の色。白ならテキストがそのまま見える
> - **`_HiddenColor`**: ライトが当たっていない部分の色。`(0,0,0,0)` = 完全に透明 → 見えない

### Vertex Shader（頂点シェーダー）

```hlsl
v2f vert(appdata v)
{
    v2f o;
    o.pos      = UnityObjectToClipPos(v.vertex);     // 3D 位置 → 画面上の位置に変換
    o.uv       = TRANSFORM_TEX(v.uv, _MainTex);      // テクスチャ座標
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz; // ワールド座標を計算
    return o;
}
```

> **解説**:
> - **頂点シェーダー**: 3D モデルの各頂点（角の点）に対して 1 回ずつ実行される。「この点は画面のどこに映るか」を計算
> - **`UnityObjectToClipPos()`**: オブジェクトのローカル座標を画面上のピクセル座標に変換する Unity の組み込み関数
> - **`o.worldPos`**: ワールド座標をフラグメントシェーダーに渡す。「このピクセルが 3D 空間のどこにあるか」をライト計算で使うため

### Fragment Shader（フラグメントシェーダー = 核心部分）

```hlsl
fixed4 frag(v2f i) : SV_Target
{
    // 1. ライトが無効ならテキストを非表示
    if (_StageSpotlightEnabled < 0.5)
        return _HiddenColor;
```

> **解説**: ライトが OFF（`_StageSpotlightEnabled = 0`）なら、即座に透明色を返す → テキストは見えない

```hlsl
    // 2. ピクセルのワールド座標からライトへのベクトルを計算
    float3 toLight = _StageSpotlightPosition.xyz - i.worldPos;
    float distance = length(toLight);        // ライトまでの距離
    float3 dirToPixel = -normalize(toLight); // ライトからピクセルの方向
```

> **解説**:
> - **`length()`**: ベクトルの長さ（= 距離）を計算する HLSL の関数
> - **`normalize()`**: ベクトルの長さを 1 にする（方向だけ残す）
> - **`-normalize(toLight)`**: 符号反転で「ライト→ピクセル」の方向に変換

```hlsl
    // 3. 距離チェック: range を超えていたら非表示
    if (distance > _StageSpotlightRange)
        return _HiddenColor;
```

> **解説**: ライトの到達距離より遠ければ、照らされていない → 透明を返す

```hlsl
    // 4. 角度チェック: スポット角度の外なら非表示
    float cosAngle = dot(_StageSpotlightDirection.xyz, dirToPixel);
    if (cosAngle < _StageSpotlightCosOuter)
        return _HiddenColor;
```

> **解説**:
> - **`dot(a, b)`（内積）**: 2 つの方向の「どれだけ同じ方向か」を返す。1.0 = 完全に同じ方向、0 = 直角、-1 = 逆方向
> - **コサインの比較**: `cosAngle < _StageSpotlightCosOuter` は「ライトの中心からの角度が外角より大きい」と同じ意味。コサインは角度が大きいほど値が小さくなるため、不等号が逆に見えるが正しい

```hlsl
    // 5. スポットライトファクターの計算（内角〜外角の間でスムーズに減衰）
    float spotFactor = saturate(
        (cosAngle - _StageSpotlightCosOuter)
        / (_StageSpotlightCosInner - _StageSpotlightCosOuter)
    );
```

> **解説**:
> - **`saturate()`**: 値を 0〜1 に制限する関数（0 以下なら 0、1 以上なら 1）
> - この計算は、外角→内角の間で **0→1 にスムーズに変化** する値を作る。内角の内側は 1.0（最大）、外角の外側は 0.0（ゼロ）

```hlsl
    // 6. 距離による減衰
    float distFactor = saturate(1.0 - distance / _StageSpotlightRange);

    // 7. 最終的な照射量 = 角度 × 距離
    float lightFactor = spotFactor * distFactor;
```

> **解説**: SpotlightSensor.cs の Exposure01 と同じ考え方。ただしこちらは **ピクセルごと** に計算している（GPU で超高速）

```hlsl
    // 8. テクスチャの色を取得
    fixed4 texColor = tex2D(_MainTex, i.uv);

    // 9. Hidden ↔ Lit をブレンド
    fixed4 finalColor = lerp(_HiddenColor, _LitColor * texColor, lightFactor);
    return finalColor;
}
```

> **解説**:
> - **`tex2D(_MainTex, i.uv)`**: テクスチャ画像からこのピクセルの色を取得
> - **`lerp(a, b, t)`**: a と b を `t` の割合で混ぜる関数。`t=0` なら a（Hidden）、`t=1` なら b（Lit）、`t=0.5` なら半々
> - つまり `lightFactor = 0`（照らされていない）→ HiddenColor（透明）、`lightFactor = 1`（照射中心）→ LitColor × テクスチャ（テキストが見える）

---

## SpotlightSensor (C#) との違い

| 項目 | SpotlightSensor (C#) | このシェーダー (GPU) |
|------|----------------------|---------------------|
| 実行場所 | CPU（メインスレッド） | GPU（グラフィックカード） |
| 判定単位 | オブジェクト 1 点 | ピクセルごと（数百万点） |
| 用途 | ゲームロジック判定 | ビジュアル表現 |
| 結果 | IsLit / Exposure01 | ピクセルの色 |

> **まとめ**: SpotlightSensor は「当たっているか？」をゲームの判定に使う。シェーダーは「照射部分だけ見えるようにする」描画に使う。**役割が違うので両方必要。**

---

## 自分のプロジェクトで実装するには

### 必要ファイル

1. **`StageSpotlightText.shader`** — シェーダー本体
2. C# 側のグローバル変数設定（`PoseTestBootstrap.cs` の該当部分 or 独自スクリプト）

### セットアップ手順

```
1. シェーダーファイルをプロジェクトにコピー

2. マテリアルを作成:
   - Shader: "Custom/StageSpotlightText" を選択
   - _LitColor: ライトが当たった時の色（例: 白）
   - _HiddenColor: 暗い時の色（例: 透明 = 0,0,0,0）

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

> **解説**: この 6 行の `SetGlobal...` がないとシェーダーが動きません。必ず毎フレーム更新すること。

### カスタマイズ例

| 用途 | やり方 |
|------|--------|
| 色を変える | `_LitColor` をマテリアルの Inspector で変更 |
| 完全に消す | `_HiddenColor = (0,0,0,0)` |
| ぼんやり残す | `_HiddenColor = (0.1, 0.1, 0.1, 0.3)` → うっすら見える |
| エッジを柔らかく | `spotFactor` に `smoothstep` を適用 |
| パルス演出 | `lightFactor` に `sin(_Time.y * frequency)` を掛ける |

---

## よくあるトラブルと対処法

| 症状 | 原因 | 対処 |
|------|------|------|
| テキストが常に非表示 | C# 側の `Shader.SetGlobal...` が実行されていない | PoseTestBootstrap がシーンにあるか確認 |
| テキストが常に表示 | `_StageSpotlightEnabled` が 0 に設定されていない | ライト無効時は `0.0` を設定する |
| 照射範囲がずれる | `spotAngle` が度ではなくラジアンで渡されている | `* Mathf.Deg2Rad` の変換を確認 |
| マテリアルで Shader が見つからない | シェーダーファイルにコンパイルエラーがある | Console ウィンドウでエラーを確認 |
