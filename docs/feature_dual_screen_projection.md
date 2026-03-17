# 機能ガイド: 二画面 Off-Axis 投影

> **難易度**: ★★★★☆  
> **再利用度**: ⭐ 高い — マッピング投影、CAVE、マルチディスプレイ展示に応用可能  
> **依存パッケージ**: URP（シェーダー部分のみ。投影行列計算自体は Built-in でも動作）

---

## この機能の概要

物理的に **L 字型に配置された 2 枚のスクリーン** に向けて、それぞれ独立したカメラから映像を投影する仕組みです。

通常の Unity カメラは「画面中央に視点がある」前提で透視投影を行いますが、プロジェクターで壁に投影する場合は **観察者の位置から見て歪みのない映像** を生成する必要があります。これを実現するのが **Off-Axis Projection（非軸投影）** です。

> **透視投影とは**: 遠くのものが小さく、近くのものが大きく見える「遠近法」のこと。カメラの見え方を決める計算方法。

> **Off-Axis（非軸）とは**: 通常のカメラは「画面の真ん中を真正面から見ている」前提ですが、Off-Axis は「画面の端や斜めから見ている」場合にも正しい映像を生成できる技術。プロジェクターで壁に映す際に必要。

---

## 物理的な配置

```
    上から見た図 (Y 軸は紙面の奥方向)

           +Z (奥)
            ↑
            │
            │  ┌─────────────────────┐
            │  │   Front Screen       │
            │  │   (正面スクリーン)     │
  ──────────┘  │   Z = +ScreenDistance │
  │            └─────────────────────┘
  │ Left Screen
  │ (左スクリーン)
  │ 90° 回転配置
  └──────────────┐
                 │
          -X ←──┼──→ +X (右)
                 │
                 👤  Viewer Origin
                     (観察者の位置)
```

- **Front Screen**: 正面。Z 軸正方向を向く。幅 2.30m、高さ 1.31m（1920:1080 比率）
- **Left Screen**: 左側面。Front Screen 左端から 90° 折れて配置。同サイズ
- **Viewer Origin**: 正面スクリーンから `viewerDistanceFromScreens`（デフォルト 2.0m）の位置

---

## データフロー図

```
PoseTestBootstrap.BuildDemoInternal() が起動時に以下を自動構築:

  1. Pose Rig (空の GameObject) を生成
                │
  2. Front Surface ─────────────┐
     (ProjectionSurface)        │
     位置: (0, 0, ScreenDistance) │
     回転: 正面向き (identity)    │
                                │
  3. Left Surface               │
     (ProjectionSurface)        │
     位置: (-W/2, 0, SD-W/2)    │
     回転: Y軸 -90°             │
                                │
  4. Viewer Origin ─────────────┤  観察者の基準位置
                                │
  5. Rotation Pivot ────────────┤  ジャイロで回転する軸
        └── 6. Tip Light        │  スポットライト
            (SpotLight)         │
                                │
  7. Display.displays[1].Activate() ← マルチディスプレイ有効化
                                │
  8. Front Camera ──────────────┤  + OffAxisProjectionCamera
     targetDisplay = 2          │    → Display 2 に出力
                                │
  9. Left Camera ───────────────┘  + OffAxisProjectionCamera
     targetDisplay = 1               → Display 1 に出力
```

---

## 関連ファイル

| ファイル | パス | 行数 | ひとことで言うと |
|---------|------|------|----------------|
| `PoseTestBootstrap.cs` | `Assets/Scripts/Pose/` | ~818 | リグ全体を自動生成する司令塔 |
| `ProjectionSurface.cs` | `Assets/Scripts/Pose/` | ~44 | 投影面の大きさと位置を定義 |
| `OffAxisProjectionCamera.cs` | `Assets/Scripts/Pose/` | ~151 | Off-Axis 投影行列を計算してカメラに適用 |
| `TestScreenVisualizer.cs` | `Assets/Scripts/Pose/` | 大 | 投影面の枠線やポインタの可視化 |

---

## 各ファイルの詳細解説

### 1. ProjectionSurface.cs — 投影面の定義

**最もシンプルなファイル（44 行）。先に読むのがおすすめ。**

```csharp
public class ProjectionSurface : MonoBehaviour
{
    [SerializeField] private float width = 5.333f;   // 投影面の横幅 (メートル)
    [SerializeField] private float height = 3f;      // 投影面の縦幅 (メートル)

    // Transform の position/rotation と width/height から 4 隅のワールド座標を計算
    public Vector3 BottomLeft  => position - right * (width/2) - up * (height/2);
    public Vector3 BottomRight => position + right * (width/2) - up * (height/2);
    public Vector3 TopLeft     => position - right * (width/2) + up * (height/2);
    public Vector3 TopRight    => position + right * (width/2) + up * (height/2);
}
```

> **解説**:
> - **`[SerializeField]`**: Inspector で値を設定できるようにする Unity の属性。`private` だけどエディタからは触れる
> - **`=>`（アロー演算子）**: 「この値を返す」という意味のショートカット記法（プロパティのゲッター）
> - **`position` / `right` / `up`**: この GameObject の Transform が持つワールド座標と方向ベクトル
> - **4 隅の計算**: 中心位置（`position`）から「右に半分の幅」「上に半分の高さ」を足し引きして 4 隅を求めている
> - `Transform` の `position` が画面の中心。`rotation` でスクリーンの向きを表す。Left Screen は Y 軸を -90° 回転させることで「左を向いた壁」になります

---

### 2. OffAxisProjectionCamera.cs — Off-Axis 投影行列の計算

**この機能の数学的な核心。**

#### Off-Axis Projection とは

通常の `Camera.fieldOfView`（視野角）で設定される透視投影は、**カメラの光軸が画面中央を通る** ことを前提としています。しかし、観察者（eye point）が画面に対して正面以外の位置にいる場合、これでは像が歪みます。

Off-Axis Projection は **任意の視点位置から、任意の矩形面を見た場合の透視投影行列** を計算します。

> **投影行列（Projection Matrix）とは**: 3D 空間の座標を 2D の画面座標に変換する 4×4 の数値の表。カメラが「何をどう映すか」を数学的に決める設計図のようなもの。

#### 計算手順 (`ApplyProjection` メソッド)

```csharp
// 1. スクリーンの 3 隅を取得
Vector3 pa = surface.BottomLeft;   // 左下
Vector3 pb = surface.BottomRight;  // 右下
Vector3 pc = surface.TopLeft;      // 左上
Vector3 pe = eyePoint.position;    // 視点（観察者の目の位置）
```

> **解説**: スクリーンの物理的な位置と、観察者がどこに立っているかを取得しています。

```csharp
// 2. スクリーン面の座標系を構築
Vector3 vr = (pb - pa).normalized;  // 右方向（左下→右下のベクトル）
Vector3 vu = (pc - pa).normalized;  // 上方向（左下→左上のベクトル）
Vector3 vn = Vector3.Cross(vr, vu); // 法線 = スクリーン面に垂直な方向
```

> **解説**:
> - **`normalized`**: ベクトルの長さを 1 にする。方向だけが欲しいので
> - **`Vector3.Cross(a, b)`（外積）**: 2 つのベクトルに対して **垂直な方向** を計算する数学の操作。右方向と上方向の両方に垂直 = スクリーン面に垂直な方向（法線）

```csharp
// 3. 視点からスクリーンまでの距離
float d = Vector3.Dot(-(pa - pe), vn);
```

> **解説**:
> - **`Vector3.Dot(a, b)`（内積）**: 2 つのベクトルの「同じ方向成分」を計算。ここでは視点からスクリーン面までの垂直距離を求めている

```csharp
// 4. near plane 上での left/right/bottom/top を計算
float left   = Vector3.Dot(vr, pa - pe) * near / d;
float right  = Vector3.Dot(vr, pb - pe) * near / d;
float bottom = Vector3.Dot(vu, pa - pe) * near / d;
float top    = Vector3.Dot(vu, pc - pe) * near / d;
```

> **解説**:
> - **near plane**: カメラが映し始める最も近い面。通常 0.1m 程度
> - ここではスクリーンの各辺が near plane 上のどこに対応するかを計算している
> - `left ≠ -right` のとき「非対称」になる → これが Off-Axis の本質！通常のカメラは `left = -right` だが、斜めから見ると左右が不均等になる

```csharp
// 5. 非対称透視投影行列を構築
Matrix4x4 projection = PerspectiveOffCenter(left, right, bottom, top, near, far);

// 6. カメラのビュー行列をスクリーン座標系に合わせる
Matrix4x4 rotation = /* vr, vu, vn の 3x3 回転行列 */;
Matrix4x4 translation = /* 視点位置への平行移動 */;

// 7. Camera に適用
camera.worldToCameraMatrix = rotation * translation;
camera.projectionMatrix    = projection;
```

> **解説**:
> - **`PerspectiveOffCenter()`**: OpenGL の `glFrustum` と同じ形式で非対称な透視投影行列を作る関数
> - **`worldToCameraMatrix`**: 「ワールド空間 → カメラ空間」の変換行列。カメラの位置と向きを表す
> - **`projectionMatrix`**: 「カメラ空間 → 画面座標」の変換行列。透視投影の形を表す
> - これらを **毎フレーム上書き** することで、Unity 標準のカメラ処理を置き換えている

---

### 3. PoseTestBootstrap.cs — リグ構築の司令塔

#### マルチディスプレイの有効化

```csharp
private void SetupDisplays()
{
    if (Display.displays.Length > 1)
        Display.displays[1].Activate();  // 2 番目のモニター
    if (Display.displays.Length > 2)
        Display.displays[2].Activate();  // 3 番目のモニター
}
```

> **解説**:
> - **`Display.displays`**: PC に接続されている全モニターの配列。`[0]` がメインモニター
> - **`Activate()`**: Unity にそのモニターを使うよう指示する。デフォルトではメインモニター以外は無効
> - **注意**: `Display.displays[0]` は常にアクティブ。`Activate()` は追加のモニターに対して呼ぶ

#### カメラの解決順序

```csharp
// カメラを見つける優先順位:
// 1. Inspector でオーバーライドカメラが設定されている → それを使用
// 2. シーン内に名前で検索 ("Main Camera", "Left Projection Camera")
// 3. autoCreateCamerasIfMissing = true なら新規生成
```

> **解説**: 柔軟な設計になっていて、既存のカメラを使い回すことも、自動生成させることもできます。

#### シェーダーグローバル変数の更新

毎フレーム `Update` でスポットライトの情報を **シェーダーグローバル変数** に書き込みます。

```csharp
Shader.SetGlobalVector("_StageSpotlightPosition", light.transform.position);
Shader.SetGlobalVector("_StageSpotlightDirection", light.transform.forward);
Shader.SetGlobalFloat("_StageSpotlightRange", light.range);
Shader.SetGlobalFloat("_StageSpotlightCosOuter", Mathf.Cos(halfAngle));
Shader.SetGlobalFloat("_StageSpotlightCosInner", Mathf.Cos(innerAngle));
Shader.SetGlobalFloat("_StageSpotlightEnabled", 1.0f);
```

> **解説**:
> - **シェーダーグローバル変数**: C# から GPU のシェーダーにデータを渡す仕組み。`Shader.SetGlobal...()` で設定すると、シーン内の **すべてのシェーダー** からその値を参照できる
> - **`Mathf.Cos(halfAngle)`**: スポットライトの角度をコサインに変換。シェーダー内の角度比較を高速にするため（コサインの比較は角度の比較より計算が軽い）
> - これにより `StageSpotlightText.shader` 等がスポットライトの位置を参照して「照射範囲内のテキストだけ表示」ができる

---

## 自分のプロジェクトで実装するには

### 最小構成（2 ファイル）

1. **`ProjectionSurface.cs`** — そのままコピー
2. **`OffAxisProjectionCamera.cs`** — そのままコピー

### セットアップ手順

```
1. スクリーンに対応する空の GameObject を作り、ProjectionSurface を付ける
   - Width/Height をスクリーンの実寸（メートル）に設定
   - Transform の位置・回転をスクリーンの物理的な配置に合わせる

2. カメラを用意し、OffAxisProjectionCamera を付ける
   - projectionSurface = 上で作った ProjectionSurface
   - eyePoint = 観察者の位置を表す Transform

3. 複数画面の場合は Camera.targetDisplay を分ける
   - Display 0 = メインモニター
   - Display 1 = 追加モニター 1

4. Play Mode で追加モニターを有効化:
   if (Display.displays.Length > 1)
       Display.displays[1].Activate();
```

> **解説**: `targetDisplay` はカメラの映像を **どのモニターに表示するか** を指定するプロパティ。

### L 字型以外の応用例

| 応用 | やり方 |
|------|--------|
| **3 面** | ProjectionSurface を 3 つ配置し、カメラも 3 台 |
| **天井** | ProjectionSurface を X 軸 -90° 回転して天井位置に配置 |
| **単一画面** | ProjectionSurface 1 つだけ。観察者が中央から外れた位置にいる場合に有効 |
| **VR CAVE** | 4〜6 面の ProjectionSurface を箱状に配置 |

### 重要な注意点

- `OffAxisProjectionCamera` は `LateUpdate` で `projectionMatrix` を上書きするため、**Cinemachine と併用する場合は実行順序に注意**

> **`LateUpdate` とは**: Unity の `Update()` の後に呼ばれる関数。カメラの最終的な位置が決まった後に投影行列を上書きしたいので、`LateUpdate` を使っている

- `flipHorizontally` / `flipVertically` はリアプロジェクション（裏側から投影）時に使用

> **リアプロジェクションとは**: スクリーンの裏側からプロジェクターで投影する方式。像が左右反転するので、シェーダーで反転を補正する
