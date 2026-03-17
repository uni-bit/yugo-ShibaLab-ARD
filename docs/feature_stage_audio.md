# 機能ガイド: ステージ音声制御 (StageAudioController)

> **難易度**: ★★☆☆☆  
> **再利用度**: ⭐ 高い — ステージ/レベルごとに BGM・SE を切り替えるプロジェクト全般に応用可能  
> **依存パッケージ**: なし（Unity 標準 API のみ）

---

## この機能の概要

**ステージごとの環境音（BGM）** と **ギミックイベントに連動した効果音（SE）** を一元管理するオーディオコントローラです。

ステージ切り替え時に環境音を自動で入れ替え、パズルクリアや特定のアクション時に対応する SE をワンショット再生します。

---

## アーキテクチャ

```
StageSequenceController
  │  TransitionToStage(index)
  ▼
StageAudioController
  ├── ambientSource     (AudioSource: ループ再生用)
  ├── sfxSource         (AudioSource: ワンショット SE 用)
  └── AudioClip マッピング
      ├── Stage 1 環境音: 森、小鳥、川、鈴虫、風
      ├── Stage 1 SE:    動物移動、葉擦れ、土移動
      ├── Stage 2 環境音: 洞窟
      ├── Stage 2 SE:    水滴、壁破壊、爆発
      ├── Stage 3 環境音: 共通環境音
      ├── Stage 3 SE:    光る音×3、小石落下、岩浮上振動
      └── 共通 SE:       パズルクリア音
```

---

## 関連ファイル

| ファイル | パス | 行数 |
|---------|------|------|
| `StageAudioController.cs` | `Assets/Scripts/Stages/` | ~800 |

---

## Inspector 設定

### AudioSource 設定

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `ambientSource` | AudioSource | BGM ループ再生用。`loop = true` にしておく |
| `sfxSource` | AudioSource | SE ワンショット用 |

### マスターボリューム

| フィールド | 型 | デフォルト | 説明 |
|-----------|-----|----------|------|
| `masterVolume` | float | `1.0` | 全体の音量スケール |
| `volumeStep` | float | `0.1` | ↑↓キーでの音量変化量 |

### ステージ別 AudioClip 設定

各ステージのクリップは `[Header("Stage N")]` セクションの `SerializeField` で Inspector から割り当てます。

---

## 主要メソッド

### `TransitionToStage(int stageIndex)` — ステージ切り替え時

```csharp
public void TransitionToStage(int stageIndex)
{
    // 1. 現在の環境音を停止
    StopAmbient();

    // 2. 新しい環境音を取得
    AudioClip newAmbient = GetAmbientClipForStage(stageIndex);

    // 3. 環境音を再生 (ループ)
    if (newAmbient != null)
    {
        ambientSource.clip = newAmbient;
        ambientSource.loop = true;
        ambientSource.volume = masterVolume;
        ambientSource.Play();
    }
}
```

### `PlaySfx(AudioClip clip)` — SE 再生

```csharp
public void PlaySfx(AudioClip clip)
{
    if (clip == null) return;
    sfxSource.PlayOneShot(clip, masterVolume);
}
```

### `PlaySfx(string eventName)` — イベント名で SE 再生

```csharp
public void PlaySfx(string eventName)
{
    AudioClip clip = ResolveSfxClip(eventName);
    PlaySfx(clip);
}
```

SE の名前解決は `switch` 文で行われ、例えば:
- `"puzzle_clear"` → パズルクリア音
- `"stage1_creature_hop"` → Stage 1 の動物跳ねる音
- `"stage2_wall_break"` → Stage 2 の壁破壊音
- `"stage3_glow_1"` → Stage 3 の光る音 (1 種目)

### マスター音量の操作

```csharp
private void Update()
{
    if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        masterVolume = Mathf.Clamp01(masterVolume + volumeStep);
    if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        masterVolume = Mathf.Clamp01(masterVolume - volumeStep);

    // リアルタイムで音量を反映
    ambientSource.volume = masterVolume;
}
```

---

## 音声ファイルの配置

```
Assets/assets/sounds/
├── stage1/
│   ├── ambient_forest.wav
│   ├── ambient_birds.wav
│   ├── se_creature_move.wav
│   └── se_leaves_rustle.wav
├── stage2/
│   ├── ambient_cave.wav
│   ├── se_water_drop.wav
│   ├── se_wall_break.wav
│   └── se_explosion.wav
├── stage3/
│   ├── ambient_common.wav
│   ├── se_glow_1.wav
│   ├── se_glow_2.wav
│   ├── se_glow_3.wav
│   ├── se_pebble_fall.wav
│   └── se_rock_rumble.wav
└── common/
    └── se_puzzle_clear.wav
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

### カスタマイズ例

| 用途 | やり方 |
|------|--------|
| クロスフェード | 2 つの AudioSource を用意して片方をフェードイン、もう片方をフェードアウト |
| 3D サウンド | AudioSource の Spatial Blend を 1.0 に |
| ランダム SE | `AudioClip[]` を用意して `Random.Range` で選択 |
| Audio Mixer 統合 | `ambientSource.outputAudioMixerGroup` を設定 |
| BGM ストリーミング | 大きな音声ファイルは Import Settings で `Streaming` にする |

---

## よくあるトラブルと対処法

| 症状 | 原因 | 対処 |
|------|------|------|
| 音が出ない | AudioClip が未割り当て | Inspector で確認 |
| 音量が 0 | masterVolume = 0 | ↑キーで上げる / デフォルト値を確認 |
| SE が重なって聞こえる | `PlayOneShot` は複数同時再生可能 | 同時再生制限を追加するか `Play` に切り替え |
| 環境音が途切れる | `loop = false` になっている | AudioSource の loop を true に |
