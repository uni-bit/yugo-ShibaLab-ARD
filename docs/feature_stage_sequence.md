# 機能ガイド: ステージ切り替え制御

> **難易度**: ★★☆☆☆  
> **再利用度**: ⭐ 高い — 複数のレベル/ステージ/ルームを管理するプロジェクト全般に応用可能  
> **依存パッケージ**: なし（Unity 標準 API のみ）

---

## この機能の概要

**複数のステージ（GameObjectのグループ）を排他的に ON/OFF し、フェード遷移で切り替える** 仕組みです。

> **「排他的に ON/OFF」とは**: 同時に 1 つだけが ON で、他は全部 OFF。テレビのチャンネル切り替えと同じイメージ。
> 
> **フェード遷移とは**: 画面が一旦暗くなって（フェードアウト）→ ステージを切り替えて → また明るくなる（フェードイン）という演出。

各ステージをルートの `GameObject` としてまとめ、`StageSequenceController` が配列で管理します。

---

## データフロー図

```
  キーボード入力（1, 2, 3, 4 キー or [ ] キー）
  または他のスクリプトからの呼び出し
                    │
                    ▼
      ┌──────────────────────────────┐
      │  StageSequenceController      │
      │  SetStage(index) または       │
      │  FadeToStage(index) を実行    │
      └──────────┬───────────────────┘
                 │
    ┌────────────┼────────────┐
    ▼            ▼            ▼
 Stage 1      Stage 2      Stage 3      Stage 4
 Active ✅    Inactive ❌  Inactive ❌  Inactive ❌
 (表示中)     (非表示)      (非表示)      (非表示)
    │
    ├── 3Dモデル
    ├── パズルギミック
    └── ライト

    同時に:
    ┌──────────────────────────┐     ┌──────────────────────────┐
    │ StageTransitionFader     │     │ StageSpotlightSettings   │
    │ 暗転 → 明転 のフェード演出 │     │ ステージ毎にライトの       │
    └──────────────────────────┘     │ 角度・範囲・明るさを変更   │
                                     └──────────────────────────┘
```

---

## 関連ファイル

| ファイル | パス | 行数 | ひとことで言うと |
|---------|------|------|----------------|
| `StageSequenceController.cs` | `Assets/Scripts/Stages/` | ~585 | ステージの ON/OFF を管理する本体 |
| `StageTransitionFader.cs` | `Assets/Scripts/Stages/` | ~100 | 黒画面のフェード演出 |
| `StageRootMarker.cs` | `Assets/Scripts/Stages/` | ~10 | 「これはステージのルート」と識別するタグ |
| `StageSpotlightSettings.cs` | `Assets/Scripts/Stages/` | ~50 | ステージごとのライト設定値 |
| `StageSequenceDebugBuilder.cs` | `Assets/Scripts/Stages/` | ~100 | 不足しているステージを自動補完 |

---

## StageSequenceController.cs の詳細

### Inspector 設定

| フィールド | 型 | 何のために設定するか |
|-----------|-----|-------------------|
| `stageRoots` | GameObject[] | ステージのルート GameObject の配列。Index 0 = Stage 1 |
| `initialStageIndex` | int | ゲーム開始時に最初に表示するステージ番号（0 始まり） |
| `fader` | StageTransitionFader | フェード演出を担当するコンポーネント |
| `bootstrap` | PoseTestBootstrap | スポットライト設定の適用先 |

> **`GameObject[]`（配列）とは**: 同じ型のデータを複数まとめて格納する入れ物。ここでは Stage 1〜4 の 4 つの GameObject を順番に入れる。

### 主要メソッド

#### `SetStage(int index)` — 即座に切り替え

```csharp
public void SetStage(int index)
{
    // 1. 全ステージを非アクティブにする
    for (int i = 0; i < stageRoots.Length; i++)
        stageRoots[i].SetActive(false);  // 見えなくする

    // 2. 指定ステージだけアクティブにする
    stageRoots[index].SetActive(true);   // 見えるようにする
    currentStageIndex = index;

    // 3. スポットライトの設定を切り替え（角度・範囲・明るさ）
    ApplySpotlightSettings(index);

    // 4. 音声コントローラに「ステージが変わったよ」と通知
    audioController?.TransitionToStage(index);
}
```

> **解説**:
> - **`SetActive(false)`**: その GameObject とすべての子オブジェクトを非表示＆無効にする Unity の関数。`true` で表示＆有効に戻る
> - **`for` ループ**: 全ステージを順番に `false` にしてから、目的のステージだけ `true` にする → 排他的な切り替え
> - **`audioController?.TransitionToStage()`**: `?.` は「audioController が null じゃなければ呼ぶ」という安全な書き方。null なら何もしない

#### `FadeToStage(int index)` — フェード付き切り替え

```csharp
public async void FadeToStage(int index)
{
    // 1. 暗転（画面が黒くなる）
    await fader.FadeOut(fadeSeconds);

    // 2. 暗転中にステージを切り替え（ユーザーには見えない）
    SetStage(index);

    // 3. 明転（黒画面が消えて新しいステージが見える）
    await fader.FadeIn(fadeSeconds);
}
```

> **解説**:
> - **`async` / `await`**: C# の非同期処理。「この処理が終わるまで待って、終わったら次に進む」という意味
> - **`await fader.FadeOut(fadeSeconds)`**: フェードアウトが完了するまで待つ。この間ゲームは止まらず、他の処理は普通に動く
> - この仕組みにより「暗転 → 切り替え → 明転」がスムーズにつながる

#### キーボード操作

```csharp
private void Update()
{
    // 数字キーでダイレクト切り替え
    if (Keyboard.current.digit1Key.wasPressedThisFrame) SetStage(0);
    if (Keyboard.current.digit2Key.wasPressedThisFrame) SetStage(1);
    if (Keyboard.current.digit3Key.wasPressedThisFrame) SetStage(2);
    if (Keyboard.current.digit4Key.wasPressedThisFrame) SetStage(3);

    // [ ] キーで前後移動
    if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
        SetStage(Mathf.Max(0, currentStageIndex - 1));              // 前のステージ
    if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
        SetStage(Mathf.Min(stageRoots.Length - 1, currentStageIndex + 1)); // 次のステージ
}
```

> **解説**:
> - **`wasPressedThisFrame`**: 「このフレームでキーが押された瞬間」だけ true。長押ししても 1 回だけ反応する
> - **`Mathf.Max(0, ...)` / `Mathf.Min(...)`**: 配列の範囲外にならないようにクランプ（制限）する。Stage 1 より前や Stage 4 より後ろには行かない

---

## StageTransitionFader.cs の詳細

黒い全画面パネルの透明度を時間で変化させるフェーダーです。

```csharp
public class StageTransitionFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;  // 黒パネルの透明度を制御

    public async Task FadeOut(float duration)
    {
        canvasGroup.gameObject.SetActive(true);  // 黒パネルを表示
        float t = 0;
        while (t < duration)
        {
            canvasGroup.alpha = t / duration;  // 0 → 1（透明 → 真っ黒）
            t += Time.deltaTime;
            await Task.Yield();  // 1フレーム待って次のループへ
        }
        canvasGroup.alpha = 1f;  // 最後にきっちり 1.0 にする
    }

    public async Task FadeIn(float duration)
    {
        float t = 0;
        while (t < duration)
        {
            canvasGroup.alpha = 1f - (t / duration);  // 1 → 0（真っ黒 → 透明）
            t += Time.deltaTime;
            await Task.Yield();
        }
        canvasGroup.alpha = 0f;
        canvasGroup.gameObject.SetActive(false);  // 黒パネルを非表示
    }
}
```

> **解説**:
> - **`CanvasGroup`**: Unity UI の透明度をまとめて制御するコンポーネント。`alpha = 0` で透明、`alpha = 1` で不透明
> - **`alpha = t / duration`**: 時間の経過に比例して 0→1 にする。`duration = 0.5` なら 0.5 秒かけてフェードアウト
> - **`Task.Yield()`**: 「1フレーム分だけ処理を譲る」。これにより while ループが 1 フレームずつ進む
> - **`Time.deltaTime`**: 前フレームからの経過秒数。これを `t` に足し続けることで経過時間を計測

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
4. initialStageIndex を設定（0 = 最初のステージ）

5. フェード演出も使う場合:
   - Canvas + CanvasGroup + 黒いパネルを作る
   - StageTransitionFader を付けて canvasGroup を割り当て
```

### カスタマイズ例

| 用途 | やり方 |
|------|--------|
| 3 ステージだけ | `stageRoots` を 3 要素にするだけ |
| 別のキーで操作 | `Update()` のキー判定を書き換え |
| ステージ遷移イベント | `onStageChanged` 等の UnityEvent を追加 |
| ロード画面を挟む | `FadeToStage` の暗転中に非同期ロードを実行 |
| 円形ループ | index の計算に `% stageRoots.Length` を使う |
