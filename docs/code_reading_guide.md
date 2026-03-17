# コードリーディングガイド — 全体構成

> **プロジェクト名**: yugo_ARD-ShibaLab  
> **Unity バージョン**: 6000.0.68f1 / URP / Input System  
> **最終更新**: 2026-03-17

---

## このプロジェクトは何か

**「ライトを使ったインタラクティブ体験」** のプロトタイプ環境です。

来場者がスマホを懐中電灯のように動かすと、L 字型に並んだ 2 枚のスクリーン上でスポットライトが追従し、暗闇の中に隠されたオブジェクトを「照らして発見する」体験ができます。Stage 1 → 2 → 3 → 4 の順にパズルを解き進む構成です。

---

## フォルダ構成マップ

```
yugo-ShibaLab-ARD/
├── 📄 README.md                ← プロジェクト全体の説明書
├── 📄 ARCHITECTURE.md          ← コンポーネント依存関係 (Mermaid 図)
├── 📄 zigsim_receiver.py       ← ZIG SIM デバッグ用 Python スクリプト
│
├── 📁 Assets/
│   ├── 📁 Scripts/
│   │   ├── 📁 Pose/            ← ジャイロ入力 & 二画面投影 (10 ファイル)
│   │   └── 📁 Stages/          ← ステージギミック (28+ ファイル)
│   │       └── 📁 Editor/      ← ステージ用 Editor 拡張
│   │
│   ├── 📁 Editor/              ← Inspector カスタムボタン (2 ファイル)
│   ├── 📁 Shaders/             ← カスタムシェーダー (1 ファイル)
│   ├── 📁 Scenes/              ← Main.unity / SampleScene.unity
│   ├── 📁 Settings/            ← URP 設定 (PC / Mobile)
│   ├── 📁 assets/              ← 3D モデル (.glb) & サウンド素材
│   │   └── 📁 sounds/          ← ステージ別 BGM・SE
│   └── 📄 Stage1Manager.cs     ← ⚠️ レガシー版 (使用しない)
│
├── 📁 docs/                    ← このガイド群
├── 📁 Packages/                ← Unity パッケージ管理
└── 📁 ProjectSettings/         ← Unity プロジェクト設定
```

---

## 実装されている機能の一覧

以下の表で、各機能の **概要** と **詳細ガイドへのリンク** を示します。  
★ マークは **他のプロジェクトでも再利用しやすい機能** で、個別の詳細ガイドを用意しています。

| # | 機能名 | 概要 | 詳細ガイド |
|---|--------|------|-----------|
| 1 | ★ スマホジャイロセンサー入力 | ZIG SIM アプリから UDP/OSC でクォータニオンを受信し、Unity 内のスポットライトの回転に反映 | [feature_gyro_sensor.md](feature_gyro_sensor.md) |
| 2 | ★ 二画面 Off-Axis 投影 | L 字配置の 2 面スクリーンに非軸投影で歪みのない映像を生成 | [feature_dual_screen_projection.md](feature_dual_screen_projection.md) |
| 3 | ★ スポットライト照射判定 | 角度・距離ベースで照射量 (0〜1) を計算するセンサー | [feature_spotlight_sensor.md](feature_spotlight_sensor.md) |
| 4 | ★ ステージ切り替え制御 | 複数ステージの on/off 管理 + フェード遷移 | [feature_stage_sequence.md](feature_stage_sequence.md) |
| 5 | ★ ステージ音声制御 | ステージごとの環境音切り替え + ギミック連動 SE | [feature_stage_audio.md](feature_stage_audio.md) |
| 6 | ★ スポットライト連動シェーダー | ライト照射位置に応じてテキストが浮かび上がる URP シェーダー | [feature_spotlight_shader.md](feature_spotlight_shader.md) |
| 7 | Stage 1: 順番照射パズル | 決められた順にターゲットへライトを当てると進行 | (後述) |
| 8 | Stage 2: 記号解読 → コードロック | 前半: 記号解読、後半: ダイヤル式コードロック | (後述) |
| 9 | Stage 3/4: カラーヒントロック | 色付き岩を照らして台座を活性化 | (後述) |
| 10 | Stage 4: エンディング演出 | 暗転→メッセージ表示→Stage 2 へ復帰 | (後述) |
| 11 | Editor 拡張 | Inspector カスタムボタン | (後述) |

---

## Pose 系ファイル一覧 (`Assets/Scripts/Pose/`)

| ファイル | 行数 | 役割 | 関連する詳細ガイド |
|---------|-----|------|--------------------|
| `PoseTestBootstrap.cs` | 818 | 投影リグ生成・ActiveSpotLight 公開 | [ジャイロ](feature_gyro_sensor.md) / [投影](feature_dual_screen_projection.md) |
| `UdpQuaternionReceiver.cs` | 1637 | UDP/OSC クォータニオン受信 (別スレッド) | [ジャイロ](feature_gyro_sensor.md) |
| `PoseRotationDriver.cs` | 199 | 受信クォータニオン → ライト回転に反映 | [ジャイロ](feature_gyro_sensor.md) |
| `PoseCalibrationCoordinator.cs` | 130 | キャリブレーション調整 | [ジャイロ](feature_gyro_sensor.md) |
| `PoseDebugOverlay.cs` | 大 | デバッグ UI (D / C / F11 キー) | — |
| `TestScreenVisualizer.cs` | 大 | 投影面とポインタの可視化 | [投影](feature_dual_screen_projection.md) |
| `ProjectionSurface.cs` | 44 | 投影面サーフェスの定義 | [投影](feature_dual_screen_projection.md) |
| `OffAxisProjectionCamera.cs` | 151 | 非軸投影カメラ行列の計算 | [投影](feature_dual_screen_projection.md) |
| `QuaternionCoordinateConverter.cs` | 216 | iOS/Android → Unity 座標系変換 | [ジャイロ](feature_gyro_sensor.md) |
| `QuaternionCalibrationUtility.cs` | 小 | 相対回転計算 (static) | [ジャイロ](feature_gyro_sensor.md) |

---

## Stage 系ファイル一覧 (`Assets/Scripts/Stages/`)

### 共通コンポーネント

| ファイル | 役割 | 関連する詳細ガイド |
|---------|------|--------------------|
| `SpotlightSensor.cs` | ライト照射量計算 (IsLit / Exposure01) | [照射判定](feature_spotlight_sensor.md) |
| `StageSequenceController.cs` | Stage 1〜4 の切り替え管理 | [ステージ制御](feature_stage_sequence.md) |
| `StageSequenceDebugBuilder.cs` | デバッグ用ステージの自動生成 | [ステージ制御](feature_stage_sequence.md) |
| `StageRootMarker.cs` | ステージ識別タグ | [ステージ制御](feature_stage_sequence.md) |
| `StageTransitionFader.cs` | ステージ遷移フェード演出 | [ステージ制御](feature_stage_sequence.md) |
| `StageSpotlightSettings.cs` | ステージ別スポットライト設定 | — |
| `StageSpotlightMaterialUtility.cs` | スポットライト用マテリアルユーティリティ | — |
| `StageAudioController.cs` | 環境音・SE 制御 | [音声制御](feature_stage_audio.md) |
| `FaceCameraBillboard.cs` | カメラ方向ビルボード | — |
| `LightReactiveRendererFeedback.cs` | ライト照射でレンダラー色変化 | — |
| `LightReactiveLineFeedback.cs` | ライト照射でライン色変化 | — |
| `LightReactiveTextFeedback.cs` | ライト照射でテキスト色変化 | — |

### Stage 1 専用

| ファイル | 役割 |
|---------|------|
| `StageLightOrderedPuzzle.cs` | 順番照射パズル制御 |
| `StageLightCreatureTarget.cs` | 個別ターゲット (Hide/Hop リアクション) |

### Stage 2 専用

| ファイル | 役割 |
|---------|------|
| `StageSymbolNumberRevealPuzzle.cs` | 前半: 記号解読パズル |
| `StageSymbolNumberRevealTarget.cs` | 前半: 個別ターゲット |
| `StageSymbolMappingDisplay.cs` | 前半: 「□=4」テキスト表示 |
| `StageLightCodeLockPuzzle.cs` | 後半: コードロック (正解 `834`) |
| `StageLightCodeDialColumn.cs` | 後半: ダイヤル桁操作 |
| `StageLightCodeDigitAnimator.cs` | 後半: 桁アニメーション |
| `StageCodeFormulaDisplay.cs` | 後半: 式ラベル表示 |
| `StageCodeLockButtonIndicator.cs` | 後半: ボタン照射インジケーター |
| `StageCodeLockRig.cs` | 後半: リグ全体制御 |
| `Stage2PuzzleController.cs` | 全体の進行調停 |
| `Stage2CompletionSequence.cs` | 完了演出 (崩落→ライト拡張→自走) |

### Stage 3/4 専用

| ファイル | 役割 |
|---------|------|
| `Stage3RockHintPuzzle.cs` | カラーヒントロックパズル (Stage 3/4 共通) |
| `Stage4SequenceController.cs` | Stage 4 エンディング＋メッセージ表示＋復帰 |

---

## ステージ別パズルの概要

### Stage 1: 順番照射パズル
決められた順にターゲットへライトを当てる。一定秒数照射で進行、間違うとリセット。全ターゲット完了で `IsSolved = true`。

### Stage 2: 記号解読 → コードロック
**前半**: 3 つのオブジェクトを照らすと「□=4」「△=3」「○=8」が浮かぶ。  
**後半**: ダイヤルの上下ボタンにライトを当てて数字を操作。正解は `834`。  
**完了演出**: パネル崩落 → ライト拡張 → 自走移動 → Stage 3 へフェード遷移。

### Stage 3/4: カラーヒントロック
隠された「緑ヒント岩」「青ヒント岩」を照らすと台座が光る。両方活性化でフィナーレ演出。Stage 3 は Stage 4 へ自動遷移、Stage 4 は最終ステージ。

### Stage 4: エンディング演出
`Stage3RockHintPuzzle` を無効化し、暗転 → 「Congratulations」メッセージ → Stage 2 へ自動復帰。複数ディスプレイ対応のオーバーレイ Canvas を生成。

---

## 操作キー一覧

| キー | 機能 |
|------|------|
| `D` | デバッグオーバーレイの表示/非表示 |
| `C` | キャリブレーションリセット |
| `F11` | フルスクリーン / ウィンドウ切り替え |
| `[` / `]` | 前/次のステージ |
| `1` `2` `3` `4` | 各ステージへ直接移動 |
| `↑` / `↓` | マスター音量調整 |

---

## おすすめ読み順

| 順番 | ファイル | 理由 |
|------|---------|------|
| 1 | `PoseTestBootstrap.cs` | 全体の起動処理。何がどう生成されるか全体像をつかめる |
| 2 | `UdpQuaternionReceiver.cs` | ジャイロデータがどう届くのか |
| 3 | `OffAxisProjectionCamera.cs` | 二画面投影の数学的な仕組み |
| 4 | `SpotlightSensor.cs` | ステージ側がライトをどう感じるか |
| 5 | `StageSequenceController.cs` | ステージの管理と切り替え |
| 6 | 各ステージのパズル | 興味のあるステージから |
