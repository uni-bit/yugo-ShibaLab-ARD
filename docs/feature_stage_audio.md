# 機能ガイド: ステージ音声制御 (StageAudioController)

> **難易度**: ★★☆☆☆  
> **再利用度**: ⭐ 高い — ステージ/レベルごとに BGM・SE を切り替えるプロジェクト全般に応用可能  
> **依存パッケージ**: なし（Unity 標準 API のみ）

---

## この機能の概要

**ステージごとの環境音（BGM）** と **ギミックイベントに連動した効果音（SE）** を一元管理するオーディオコントローラです。

> **BGM（Background Music）とは**: 背景音楽。ステージ中ずっとループ再生される音（森の音、洞窟の音など）
> 
> **SE（Sound Effect）とは**: 効果音。特定のアクション時に 1 回だけ鳴る音（パズルクリア、壁破壊など）

ステージ切り替え時に環境音を自動で入れ替え、パズルクリアや特定のアクション時に対応する SE をワンショット再生します。

> **ワンショット再生とは**: 「1 回だけ再生して終わり」という再生方法。BGM のようにループしない。

---

## データフロー図

```
StageSequenceController
  │  ステージが変わったとき
  │  TransitionToStage(index) を呼ぶ
  │
  ▼
┌──────────────────────────────────┐
│  StageAudioController             │
│                                   │
│  ├── ambientSource (AudioSource)  │  ← BGM ループ再生用
│  │    loop = true                 │
│  │                                │
│  └── sfxSource (AudioSource)      │  ← SE ワンショット用
│       loop = false                │
│                                   │
│  AudioClip マッピング:              │
│  ├── Stage 1: 森、小鳥、川...      │
│  ├── Stage 2: 洞窟の環境音         │
│  ├── Stage 3/4: 共通環境音         │
│  └── 共通 SE: パズルクリア音        │
└──────────────────────────────────┘
```

> **`AudioSource` とは**: Unity で音を再生するコンポーネント。スピーカーのようなもの。BGM 用と SE 用で 2 つ使い分けている。
> 
> **`AudioClip` とは**: 音声データそのもの（.wav, .mp3 ファイル）。`AudioSource` にセットして再生する。

---

## 関連ファイル

| ファイル | パス | 行数 |
|---------|------|------|
| `StageAudioController.cs` | `Assets/Scripts/Stages/` | ~800 |

---

## Inspector 設定

| フィールド | 型 | 何のために設定するか |
|-----------|-----|-------------------|
| `ambientSource` | AudioSource | BGM ループ再生用のスピーカー。`loop = true` にしておくこと |
| `sfxSource` | AudioSource | SE ワンショット用のスピーカー |
| `masterVolume` | float (0〜1) | 全体の音量。1.0 = 最大、0.0 = 無音 |
| `volumeStep` | float | ↑↓キーでの音量変化量（デフォルト: 0.1 = 10% ずつ変化） |
| 各ステージの AudioClip | AudioClip | Inspector から直接音声ファイルをドラッグ&ドロップで割り当て |

---

## 主要メソッドの解説

### `TransitionToStage(int stageIndex)` — ステージ切り替え時

```csharp
public void TransitionToStage(int stageIndex)
{
    // 1. 今流れている環境音を停止
    StopAmbient();

    // 2. 新しいステージの環境音を取得
    AudioClip newAmbient = GetAmbientClipForStage(stageIndex);

    // 3. 環境音を再生（ループ = ずっと繰り返す）
    if (newAmbient != null)
    {
        ambientSource.clip = newAmbient;    // 再生する音声をセット
        ambientSource.loop = true;          // ループ ON
        ambientSource.volume = masterVolume; // 音量を設定
        ambientSource.Play();               // 再生開始
    }
}
```

> **解説**:
> - **`StopAmbient()`**: 前のステージの BGM を止める。いきなり次の BGM を Play すると音が重なってしまう
> - **`ambientSource.clip = newAmbient`**: AudioSource に「次に再生する音声ファイル」をセットする
> - **`ambientSource.loop = true`**: 音声の末尾まで再生したら自動で先頭に戻って繰り返す
> - **`ambientSource.Play()`**: 実際に再生を開始する

### `PlaySfx(AudioClip clip)` — SE 再生

```csharp
public void PlaySfx(AudioClip clip)
{
    if (clip == null) return;       // 音声が未設定なら何もしない
    sfxSource.PlayOneShot(clip, masterVolume);  // 1回だけ再生
}
```

> **解説**:
> - **`PlayOneShot(clip, volume)`**: `Play()` と違い、今再生中の音を止めずに **重ねて再生** できる。複数の SE が同時に鳴っても大丈夫
> - 例: パズルクリア音と爆発音が同時に鳴る場合、`PlayOneShot` なら両方聞こえる

### `PlaySfx(string eventName)` — イベント名で SE 再生

```csharp
public void PlaySfx(string eventName)
{
    AudioClip clip = ResolveSfxClip(eventName);  // 名前から AudioClip を探す
    PlaySfx(clip);
}
```

> **解説**: 文字列（イベント名）で SE を呼び出せる便利メソッド。呼び出し側は音声ファイルの詳細を知らなくていい。

SE の名前と対応する音：

| イベント名 | 対応する音 |
|-----------|----------|
| `"puzzle_clear"` | パズルクリア音 |
| `"stage1_creature_hop"` | Stage 1：動物が跳ねる音 |
| `"stage2_wall_break"` | Stage 2：壁が壊れる音 |
| `"stage3_glow_1"` | Stage 3：光る音（1種目） |

### マスター音量の操作

```csharp
private void Update()
{
    // ↑キーで音量アップ
    if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        masterVolume = Mathf.Clamp01(masterVolume + volumeStep);
    // ↓キーで音量ダウン
    if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        masterVolume = Mathf.Clamp01(masterVolume - volumeStep);

    // リアルタイムで BGM の音量に反映
    ambientSource.volume = masterVolume;
}
```

> **解説**:
> - **`Mathf.Clamp01(value)`**: 値を 0.0〜1.0 の範囲に制限する関数。0 以下にはならないし、1 以上にもならない
> - **`volumeStep = 0.1`**: 1 回キーを押すと 10% 変化する

---

## 音声ファイルの配置

```
Assets/assets/sounds/
├── stage1/
│   ├── ambient_forest.wav       ← 森の環境音
│   ├── ambient_birds.wav        ← 小鳥のさえずり
│   ├── se_creature_move.wav     ← 動物が動く音
│   └── se_leaves_rustle.wav     ← 葉擦れの音
├── stage2/
│   ├── ambient_cave.wav         ← 洞窟の環境音
│   ├── se_water_drop.wav        ← 水滴の音
│   └── se_wall_break.wav        ← 壁が壊れる音
├── stage3/
│   ├── ambient_common.wav       ← 共通環境音
│   ├── se_glow_1.wav            ← 光る音
│   └── se_rock_rumble.wav       ← 岩が浮上する振動音
└── common/
    └── se_puzzle_clear.wav      ← パズルクリア音（全ステージ共通）
```

---

## 自分のプロジェクトで実装するには

### 最小構成（1 ファイル）

`StageAudioController.cs` をベースにカスタマイズ。

### セットアップ手順

```
1. AudioSource を 2 つ持つ GameObject を作る
   - Ambient 用: loop = true, playOnAwake = false
   - SFX 用:     loop = false, playOnAwake = false

2. StageAudioController を付けて AudioSource を割り当て

3. Inspector で各ステージの AudioClip を割り当て

4. StageSequenceController の SetStage() 内で呼ぶ:
   audioController.TransitionToStage(stageIndex);

5. パズルクリア等のイベント時:
   audioController.PlaySfx("puzzle_clear");
```

> **`playOnAwake = false` とは**: ゲーム開始時に勝手に再生を始めない設定。通常は false にしておき、コードから `Play()` で明示的に再生開始する。

### カスタマイズ例

| 用途 | やり方 |
|------|--------|
| クロスフェード | 2 つの AudioSource を用意して片方をフェードイン、もう片方をフェードアウト |
| 3D サウンド | AudioSource の Spatial Blend を 1.0 に |
| ランダム SE | `AudioClip[]` を用意して `Random.Range` で選択 |

---

## よくあるトラブルと対処法

| 症状 | 原因 | 対処 |
|------|------|------|
| 音が出ない | AudioClip が Inspector で未割り当て | Inspector で音声ファイルが設定されているか確認 |
| 音量が 0 | masterVolume = 0 | ↑キーで上げる or デフォルト値を確認 |
| SE が重なって聞こえる | `PlayOneShot` は複数同時再生可能（仕様通り） | 意図通りなら OK。制限したければ再生中チェックを追加 |
| 環境音が途切れる | AudioSource の `loop = false` になっている | Inspector で `loop = true` に |
