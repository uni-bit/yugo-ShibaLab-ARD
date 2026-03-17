# 機能ガイド: 二画面 Off-Axis 投影

> **難易度**: ★★★★☆  
> **再利用度**: ⭐ 高い — マッピング投影、CAVE、マルチディスプレイ展示に応用可能  
> **依存パッケージ**: URP（シェーダー部分のみ。投影行列計算自体は Built-in でも動作）

---

## この機能の概要

物理的に **L 字型に配置された 2 枚のスクリーン** に向けて、それぞれ独立したカメラから映像を投影する仕組みです。

通常の Unity カメラは「画面中央に視点がある」前提で透視投影を行いますが、プロジェクターで壁に投影する場合は **観察者の位置から見て歪みのない映像** を生成する必要があります。これを実現するのが **Off-Axis Projection（非軸投影）** です。

---

## 物理的な配置

```
    上から見た図 (Y 軸は紙面奥方向)

           +Z
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
          ─X ←──┼──→ +X
                 │
                 👤  Viewer Origin
                     (観察者の位置)
```

- **Front Screen**: 正面。Z 軸正方向を向く。幅 2.30m、高さ 1.31m (1920:1080 比率)
- **Left Screen**: 左側面。Front Screen 左端から 90° 折れて配置。同サイズ
- **Viewer Origin**: 正面スクリーンから `viewerDistanceFromScreens` (デフォルト 2.0m) の位置

---

## データフロー図

```
PoseTestBootstrap.BuildDemoInternal()
│
├── 1. Pose Rig (空 GameObject) を生成
│
├── 2. Front Surface (ProjectionSurface) を生成
│       位置: (0, 0, ScreenDistance)
│       回転: 正面向き (identity)
│
├── 3. Left Surface (ProjectionSurface) を生成
│       位置: (-Width/2, 0, ScreenDistance - Width/2)
│       回転: Y軸 -90°
│
├── 4. Viewer Origin を生成
│       位置: (-Width/2 + distance, 0, ScreenDistance - distance)
│
├── 5. Rotation Pivot を生成 (ジャイロで回転する軸)
│       位置: Viewer Origin と同じ
│       向き: Front/Left の中間点を向く
│
├── 6. Tip Light (SpotLight) を Rotation Pivot の子に生成
│
├── 7. Display.displays[1].Activate() でマルチディスプレイ有効化
│
├── 8. Front Camera を解決(既存検索 or 自動生成)
│       + OffAxisProjectionCamera を付与
│       + targetDisplay = 2 (frontCameraTargetDisplay)
│
└── 9. Left Camera を解決
        + OffAxisProjectionCamera を付与
        + targetDisplay = 1 (leftCameraTargetDisplay)
```

---

## 関連ファイル

| ファイル | パス | 行数 | 役割 |
|---------|------|------|------|
| `PoseTestBootstrap.cs` | `Assets/Scripts/Pose/` | ~818 | リグ全体の構築と管理 |
| `ProjectionSurface.cs` | `Assets/Scripts/Pose/` | ~44 | 投影面の Width/Height と 4 隅座標の公開 |
| `OffAxisProjectionCamera.cs` | `Assets/Scripts/Pose/` | ~151 | 非軸投影行列の計算と適用 |
| `TestScreenVisualizer.cs` | `Assets/Scripts/Pose/` | 大 | 投影面の枠線やポインタの可視化 |

---

## 各ファイルの詳細解説

### 1. ProjectionSurface.cs — 投影面の定義

**最もシンプルなファイル (44 行)。先に読むのがおすすめ。**

```csharp
public class ProjectionSurface : MonoBehaviour
{
    [SerializeField] private float width = 5.333f;
    [SerializeField] private float height = 3f;

    // Transform の position/rotation と width/height から 4 隅のワールド座標を計算
    public Vector3 BottomLeft  => position - right * (width/2) - up * (height/2);
    public Vector3 BottomRight => position + right * (width/2) - up * (height/2);
    public Vector3 TopLeft     => position - right * (width/2) + up * (height/2);
    public Vector3 TopRight    => position + right * (width/2) + up * (height/2);
}
```

`Transform` の `position` が画面の中心。`rotation` でスクリーンの向きを表す。Left Screen は Y 軸を -90° 回転させることで「左を向いた壁」になります。

---

### 2. OffAxisProjectionCamera.cs — 非軸投影行列の計算

**この機能の数学的な核心。**

#### Off-Axis Projection とは

通常の `Camera.fieldOfView` で設定される透視投影は **カメラの光軸が画面中央を通る** ことを前提としています。しかし、観察者（eye point）が画面に対して正面以外の位置にいる場合、これでは像が歪みます。

Off-Axis Projection は **任意の視点位置から、任意の矩形面を見た場合の透視投影行列** を計算します。

#### 計算手順 (`ApplyProjection` メソッド)

```csharp
// 1. スクリーンの 3 隅を取得
Vector3 pa = surface.BottomLeft;   // 左下
Vector3 pb = surface.BottomRight;  // 右下
Vector3 pc = surface.TopLeft;      // 左上
Vector3 pe = eyePoint.position;    // 視点

// 2. スクリーン面の座標系を構築
Vector3 vr = (pb - pa).normalized;  // 右方向
Vector3 vu = (pc - pa).normalized;  // 上方向
Vector3 vn = Cross(vr, vu);         // 法線 (手前方向)

// 3. 視点からスクリーンまでの距離
float d = Dot(-(pa - pe), vn);

// 4. 視点から各隅への方向をスクリーン座標系で投影
//    = near plane 上での left/right/bottom/top を計算
float left   = Dot(vr, pa - pe) * near / d;
float right  = Dot(vr, pb - pe) * near / d;
float bottom = Dot(vu, pa - pe) * near / d;
float top    = Dot(vu, pc - pe) * near / d;

// 5. 非対称透視投影行列を構築
Matrix4x4 projection = PerspectiveOffCenter(left, right, bottom, top, near, far);

// 6. カメラのビュー行列をスクリーン座標系に合わせる
Matrix4x4 rotation = [vr, vu, vn] の 3x3 回転行列;
Matrix4x4 translation = 視点位置への平行移動;

// 7. Camera に適用
camera.worldToCameraMatrix = rotation * translation;
camera.projectionMatrix    = projection;
```

#### PerspectiveOffCenter の行列

```
| 2n/(r-l)    0      (r+l)/(r-l)      0         |
|    0     2n/(t-b)  (t+b)/(t-b)      0         |
|    0        0     -(f+n)/(f-n)  -2fn/(f-n)     |
|    0        0         -1            0          |
```

これは `glFrustum` と同じ式です。`left ≠ -right` のとき「非対称」になり、これが Off-Axis の本質です。

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

**注意**: `Display.displays[0]` は常にアクティブ。`Activate()` は追加のモニターに対して呼びます。

#### カメラの解決順序

```
1. Inspector でオーバーライドカメラが設定されている → それを使用
2. シーン内に名前で検索 ("Main Camera", "Left Projection Camera")
3. autoCreateCamerasIfMissing = true なら新規生成
```

#### シェーダーグローバル変数の更新

毎フレーム `Update` でスポットライトの情報をシェーダーグローバル変数に書き込みます。これにより `StageSpotlightText.shader` 等がスポットライトの位置を参照できます。

```csharp
Shader.SetGlobalVector("_StageSpotlightPosition", light.transform.position);
Shader.SetGlobalVector("_StageSpotlightDirection", light.transform.forward);
Shader.SetGlobalFloat("_StageSpotlightRange", light.range);
Shader.SetGlobalFloat("_StageSpotlightCosOuter", Cos(halfAngle));
Shader.SetGlobalFloat("_StageSpotlightCosInner", Cos(innerAngle));
Shader.SetGlobalFloat("_StageSpotlightEnabled", 1.0);
```

---

## 自分のプロジェクトで実装するには

### 最小構成（2 ファイル）

1. **`ProjectionSurface.cs`** — そのままコピー
2. **`OffAxisProjectionCamera.cs`** — そのままコピー

### セットアップ手順

```
1. スクリーンに対応する 空の GameObject を作り、ProjectionSurface を付ける
   - Width/Height をスクリーンの実寸 (メートル) に設定
   - Transform の位置・回転をスクリーンの物理的な配置に合わせる

2. カメラを用意し、OffAxisProjectionCamera を付ける
   - projectionSurface = 上で作った ProjectionSurface
   - eyePoint = 観察者の位置を表す Transform

3. 複数画面の場合は Camera.targetDisplay を分ける
   - Display 0 = メインモニター
   - Display 1 = 追加モニター 1
   - Display 2 = 追加モニター 2

4. Play Mode で追加モニターを有効化:
   if (Display.displays.Length > 1)
       Display.displays[1].Activate();
```

### L 字型以外の応用例

| 応用 | やり方 |
|------|--------|
| **3 面** | ProjectionSurface を 3 つ配置し、カメラも 3 台 |
| **天井** | ProjectionSurface を Y 軸 -90° 回転して天井位置に配置 |
| **単一画面** | ProjectionSurface 1 つだけ。観察者が中央から外れた位置にいる場合に有効 |
| **VR CAVE** | 4〜6 面の Projection Surface を箱状に配置 |

### 重要な注意点

- `OffAxisProjectionCamera` は `LateUpdate` で `projectionMatrix` を上書きするため、**Cinemachine と併用する場合は実行順序に注意**。このプロジェクトでは `[DefaultExecutionOrder(1000)]` で Cinemachine より後に実行されるようにしています。
- `flipHorizontally` / `flipVertically` はリアプロジェクション（裏側から投影）時に使用します。
