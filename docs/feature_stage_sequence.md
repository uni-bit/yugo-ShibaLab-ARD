# 機能ガイド: ステージ切り替え制御

> **難易度**: ★★☆☆☆  
> **再利用度**: ⭐ 高い — 複数のレベル/ステージ/ルームを管理するプロジェクト全般に応用可能  
> **依存パッケージ**: なし（Unity 標準 API のみ）

---

## この機能の概要

**複数のステージ（GameObjectのグループ）を排他的に ON/OFF し、フェード遷移で切り替える** 仕組みです。

各ステージをルートの `GameObject` としてまとめ、`StageSequenceController` が配列で管理します。ステージ切り替え時には対象だけを `SetActive(true)` にし、他を `false` にします。オプションでフェード（暗転 → 切り替え → 明転）を挟むことができます。

---

## データフロー図

```
                        キーボード入力 / スクリプトからの呼び出し
                               │
                               ▼
                   ┌──────────────────────────┐
                   │  StageSequenceController   │
                   │  SetStage(index) or        │
                   │  FadeToStage(index)         │
                   └────────┬─────────────────┘
                            │
    ┌───────────────────────┼───────────────────────┐
    ▼                       ▼                       ▼
 Stage 1 root           Stage 2 root            Stage 3 root
 SetActive(true)        SetActive(false)        SetActive(false)
    │                       │                       │
    ├── パズルギミック        ├── ...                  ├── ...
    ├── 3Dモデル             │                       │
    └── ライト              │                       │

    ▼                       ▼
 StageTransitionFader    StageSpotlightSettings
 (暗転→明転フェード)      (ステージごとのライト角度/範囲を適用)
```

---

## 関連ファイル

| ファイル | パス | 行数 | 役割 |
|---------|------|------|------|
| `StageSequenceController.cs` | `Assets/Scripts/Stages/` | ~585 | ステージ配列管理・切り替えロジック |
| `StageTransitionFader.cs` | `Assets/Scripts/Stages/` | ~100 | フェード演出（黒画面の表示/非表示） |
| `StageRootMarker.cs` | `Assets/Scripts/Stages/` | ~10 | ステージ root を識別するタグ |
| `StageSpotlightSettings.cs` | `Assets/Scripts/Stages/` | ~50 | ステージ別のスポットライト設定 |
| `StageSequenceDebugBuilder.cs` | `Assets/Scripts/Stages/` | ~100 | 不足しているステージを自動生成 |
| `StageSequenceControllerEditor.cs` | `Assets/Editor/` | ~50 | Inspector 拡張（ボタン追加） |

---

## StageSequenceController.cs の詳細

### Inspector 設定

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `stageRoots` | GameObject[] | ステージルートの配列。Index 0 = Stage 1 |
| `initialStageIndex` | int | ゲーム開始時のステージ番号 (0-based) |
| `fader` | StageTransitionFader | フェード演出コンポーネント |
| `bootstrap` | PoseTestBootstrap | ライト設定の適用先 |

### 主要メソッド

#### `SetStage(int index)` — 即座に切り替え

```csharp
public void SetStage(int index)
{
    // 1. 全ステージを非アクティブに
    for (int i = 0; i < stageRoots.Length; i++)
        stageRoots[i].SetActive(false);

    // 2. 指定ステージだけアクティブに
    stageRoots[index].SetActive(true);
    currentStageIndex = index;

    // 3. スポットライト設定を適用
    ApplySpotlightSettings(index);

    // 4. 音声コントローラに通知
    audioController?.TransitionToStage(index);
}
```

#### `FadeToStage(int index)` — フェード付き切り替え

```csharp
public async void FadeToStage(int index)
{
    // 1. 暗転 (フェードアウト)
    await fader.FadeOut(fadeSeconds);

    // 2. ステージ切り替え (暗転中に実行)
    SetStage(index);

    // 3. 明転 (フェードイン)
    await fader.FadeIn(fadeSeconds);
}
```

#### キーボード操作

```csharp
private void Update()
{
    if (Keyboard.current.digit1Key.wasPressedThisFrame) SetStage(0);
    if (Keyboard.current.digit2Key.wasPressedThisFrame) SetStage(1);
    if (Keyboard.current.digit3Key.wasPressedThisFrame) SetStage(2);
    if (Keyboard.current.digit4Key.wasPressedThisFrame) SetStage(3);

    if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
        SetStage(Mathf.Max(0, currentStageIndex - 1));
    if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
        SetStage(Mathf.Min(stageRoots.Length - 1, currentStageIndex + 1));
}
```

---

## StageTransitionFader.cs の詳細

黒い全画面 UI パネルの不透明度を Lerp で変化させるシンプルなフェーダーです。

```csharp
public class StageTransitionFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;  // 黒パネル

    public async Task FadeOut(float duration)
    {
        canvasGroup.gameObject.SetActive(true);
        float t = 0;
        while (t < duration)
        {
            canvasGroup.alpha = t / duration;  // 0 → 1 (透明→黒)
            t += Time.deltaTime;
            await Task.Yield();
        }
        canvasGroup.alpha = 1f;
    }

    public async Task FadeIn(float duration)
    {
        float t = 0;
        while (t < duration)
        {
            canvasGroup.alpha = 1f - (t / duration);  // 1 → 0 (黒→透明)
            t += Time.deltaTime;
            await Task.Yield();
        }
        canvasGroup.alpha = 0f;
        canvasGroup.gameObject.SetActive(false);
    }
}
```

---

## StageSpotlightSettings.cs の詳細

各ステージの `StageRootMarker` に付けて使います。ステージ切り替え時に自動でライト設定が適用されます。

```csharp
[System.Serializable]
public class StageSpotlightSettings : MonoBehaviour
{
    public float spotAngle = 45f;
    public float innerSpotAngle = 35f;
    public float range = 15f;
    public float intensity = 3f;
}
```

---

## 自分のプロジェクトで実装するには

### 最小構成（2 ファイル）

1. **`StageSequenceController.cs`** — ステージ管理本体
2. **`StageTransitionFader.cs`** — フェード演出（不要なら省略可）

### セットアップ手順

```
1. シーンの Hierarchy で各ステージのルート GameObject を作る
   Stage1Root/
     ├── 3D モデル
     ├── パズルギミック
     └── ライト

   Stage2Root/
     └── ...

2. 空の GameObject に StageSequenceController を付ける
3. stageRoots 配列に各ルートをドラッグ&ドロップ
4. initialStageIndex を設定 (0 = 最初のステージ)

5. フェード演出も使う場合:
   - Canvas + CanvasGroup + 黒いパネルを作る
   - StageTransitionFader を付けて canvasGroup を割り当て
   - StageSequenceController の fader に割り当て
```

### カスタマイズ例

| 用途 | やり方 |
|------|--------|
| 3 ステージだけ | `stageRoots` を 3 要素にするだけ |
| 別のキーで操作 | `Update()` のキー判定を書き換え |
| ステージ遷移イベント | `onStageChanged` 等の UnityEvent を追加 |
| ロード画面を挟む | `FadeToStage` の暗転中に非同期ロードを実行 |
| 円形ループ | index の計算に `% stageRoots.Length` を使う |
