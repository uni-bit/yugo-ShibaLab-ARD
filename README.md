# yugo_ARD-ShibaLab

このプロジェクトは、Unity 上で動くライト照射型のインタラクション試作環境です。

大きく 2 つの要素で構成されています。

- 姿勢入力とスポットライト投影を扱う Pose / Projection 系
- スポットライト照射で進行する Stage 1 / Stage 2 / Stage 3 / Stage 4 のギミック系

現在はデモ用の実装が中心で、単一シーン内でステージを切り替えながら確認する構成になっています。

## 開発環境

- Unity 6000.0.68f1
- Universal Render Pipeline
- Input System

参照: [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt), [Packages/manifest.json](Packages/manifest.json)

## このプロジェクトでできること

- 姿勢入力に応じてスポットライト付きのポインタを動かす
- 実行時に投影用リグを自動構築する
- 黒背景上で、ライトが当たっている箇所だけを見せる演出を試す
- Stage 1, 2, 3, 4 を切り替えて、ライト照射ギミックを検証する
- Edit Mode でもプレビューを見ながら配置調整する

## 全体構成

### Pose / Projection 系

中心になるのは [Assets/Scripts/Pose/PoseTestBootstrap.cs](Assets/Scripts/Pose/PoseTestBootstrap.cs) です。

このコンポーネントは以下を担当します。

- 実行時の投影リグ自動生成
- ビューア原点と投影面の生成
- 2 台のカメラ設定
- スポットライト生成
- 姿勢入力系コンポーネントの接続
- 実行時の黒背景化と不要ライト無効化
- スポットライトのシェーダーグローバル変数の毎フレーム更新

このスクリプトが生成したスポットライトは `ActiveSpotLight` として公開され、ステージ側の [Assets/Scripts/Stages/SpotlightSensor.cs](Assets/Scripts/Stages/SpotlightSensor.cs) から参照されます。

関連する代表スクリプト:

- [Assets/Scripts/Pose/PoseTestBootstrap.cs](Assets/Scripts/Pose/PoseTestBootstrap.cs)
- [Assets/Scripts/Pose/PoseDebugOverlay.cs](Assets/Scripts/Pose/PoseDebugOverlay.cs)
- [Assets/Scripts/Pose/UdpQuaternionReceiver.cs](Assets/Scripts/Pose/UdpQuaternionReceiver.cs)
- [Assets/Scripts/Pose/PoseRotationDriver.cs](Assets/Scripts/Pose/PoseRotationDriver.cs)
- [Assets/Scripts/Pose/PoseCalibrationCoordinator.cs](Assets/Scripts/Pose/PoseCalibrationCoordinator.cs)
- [Assets/Scripts/Pose/TestScreenVisualizer.cs](Assets/Scripts/Pose/TestScreenVisualizer.cs)
- [Assets/Scripts/Pose/ProjectionSurface.cs](Assets/Scripts/Pose/ProjectionSurface.cs)
- [Assets/Scripts/Pose/OffAxisProjectionCamera.cs](Assets/Scripts/Pose/OffAxisProjectionCamera.cs)
- [Assets/Scripts/Pose/QuaternionCoordinateConverter.cs](Assets/Scripts/Pose/QuaternionCoordinateConverter.cs)

### ステージ系

ステージ切り替えの中心は [Assets/Scripts/Stages/StageSequenceController.cs](Assets/Scripts/Stages/StageSequenceController.cs) です。

このコンポーネントは以下を担当します。

- Stage 1 / 2 / 3 / 4 の root 管理
- 初期表示ステージの切り替え
- キーボードによるステージ移動
- 現在アクティブなステージ配下のライト反応処理の再評価
- 編集時のステージ自動同期

ステージ内容の自動生成は [Assets/Scripts/Stages/StageSequenceDebugBuilder.cs](Assets/Scripts/Stages/StageSequenceDebugBuilder.cs) が担当します。

この builder は以下の用途で使います。

- stage root が足りないときの自動生成
- Stage 1 / 2 / 3 / 4 のデバッグ用初期配置作成
- 既存 stage root の再利用
- Prefab を stage roots に入れた場合のシーン内インスタンス化

stage root の識別には [Assets/Scripts/Stages/StageRootMarker.cs](Assets/Scripts/Stages/StageRootMarker.cs) を使います。

## 各ステージの実装

### Stage 1

Stage 1 は「順番に対象へライトを当てる」ギミックです。

- 実装: [Assets/Scripts/Stages/StageLightOrderedPuzzle.cs](Assets/Scripts/Stages/StageLightOrderedPuzzle.cs)
- 個別ターゲット: [Assets/Scripts/Stages/StageLightCreatureTarget.cs](Assets/Scripts/Stages/StageLightCreatureTarget.cs)

仕組み:

- SpotlightSensor が各ターゲットへの照射を判定する
- 現在の正解ターゲットに一定時間ライトを当てると進行する
- 間違った対象を照らすと、設定に応じて進行をリセットする

### Stage 2

Stage 2 は 2 段階構成のギミックです。

**前半: 記号・数字対応の解読**

- 実装: [Assets/Scripts/Stages/StageSymbolNumberRevealPuzzle.cs](Assets/Scripts/Stages/StageSymbolNumberRevealPuzzle.cs)
- 個別ターゲット: [Assets/Scripts/Stages/StageSymbolNumberRevealTarget.cs](Assets/Scripts/Stages/StageSymbolNumberRevealTarget.cs)
- 表示制御: [Assets/Scripts/Stages/StageSymbolMappingDisplay.cs](Assets/Scripts/Stages/StageSymbolMappingDisplay.cs)

各 Stage2 Object に SpotlightSensor が付いており、照射量 (`Exposure01`) に応じて `□=4`, `△=3`, `○=8` のテキストが発光する。3 つすべてに照射すると後半へ進む。

**後半: コードロック**

- 実装: [Assets/Scripts/Stages/StageLightCodeLockPuzzle.cs](Assets/Scripts/Stages/StageLightCodeLockPuzzle.cs)
- 桁操作: [Assets/Scripts/Stages/StageLightCodeDialColumn.cs](Assets/Scripts/Stages/StageLightCodeDialColumn.cs)
- 桁アニメーション: [Assets/Scripts/Stages/StageLightCodeDigitAnimator.cs](Assets/Scripts/Stages/StageLightCodeDigitAnimator.cs)
- 式表示: [Assets/Scripts/Stages/StageCodeFormulaDisplay.cs](Assets/Scripts/Stages/StageCodeFormulaDisplay.cs)
- ボタン表示: [Assets/Scripts/Stages/StageCodeLockButtonIndicator.cs](Assets/Scripts/Stages/StageCodeLockButtonIndicator.cs)
- リグ全体制御: [Assets/Scripts/Stages/StageCodeLockRig.cs](Assets/Scripts/Stages/StageCodeLockRig.cs)

各列の上下ボタンにライトを当てると数字が増減する。正解コードは `834`。正解すると式ラベルの色が変わり、ドアが上方向へ開く。

**Stage 2 全体の調停と完了演出**

- 全体調停: [Assets/Scripts/Stages/Stage2PuzzleController.cs](Assets/Scripts/Stages/Stage2PuzzleController.cs)
- 完了演出: [Assets/Scripts/Stages/Stage2CompletionSequence.cs](Assets/Scripts/Stages/Stage2CompletionSequence.cs)

`Stage2PuzzleController` が「解読パズル完了」→「コードロック有効化」→「コードロック解除」→「完了演出 (`Stage2CompletionSequence.Play()`)」の順に制御する。完了演出ではパネルの崩落・ライト拡張・stage root の自走移動・Stage 3 への遷移が走る。

### Stage 3 / Stage 4

Stage 3 および Stage 4 は「カラーヒントロック」ギミックです（同じ `Stage3RockHintPuzzle` クラスを使用）。

- 実装: [Assets/Scripts/Stages/Stage3RockHintPuzzle.cs](Assets/Scripts/Stages/Stage3RockHintPuzzle.cs)

仕組み:

- 3 色の台座岩（赤・緑・青）が並ぶ
- 隠された「緑ヒント岩」と「青ヒント岩」をライトで照らすと該当色の台座岩が光る
- 両方を活性化するとフィナーレ演出（LocalReveal → PanoramaReveal → 次ステージ遷移）が走る

**Stage 3** は `advanceToNextStage = true`（Stage 4 へ自動遷移）。  
**Stage 4** は `advanceToNextStage = false`（最終ステージ、遷移しない）。

> **注意**: `StageLightCodeLockPuzzle` は Stage 2 のコードロック専用です。README の旧版では「Stage 3 = コードロック」と記載されていましたが、現在の Stage 3 は `Stage3RockHintPuzzle` です。

## ライト判定の仕組み

ライト照射判定は [Assets/Scripts/Stages/SpotlightSensor.cs](Assets/Scripts/Stages/SpotlightSensor.cs) で共通化しています。

主な役割:

- PoseTestBootstrap が生成した `ActiveSpotLight` を自動解決する
- サンプル位置に対して距離と角度から露出量を計算する
- `IsLit` と `Exposure01` を毎フレーム更新する
- 必要なら line of sight 判定も行う

Stage 2 の文字発光も、Stage 1 の進行判定も、Stage 2 コードロックの上下ボタン操作も、Stage 3/4 の岩ヒント検知も、このセンサーを基準にしています。

## 操作方法

### 共通キー

- `D`: デバッグオーバーレイの表示切り替え
- `C`: キャリブレーションや表示基準のリセット
- `F11`: ウィンドウ / フルスクリーン切り替え

### ステージ切り替えキー

- `[` : 前のステージ
- `]` : 次のステージ
- `1` : Stage 1
- `2` : Stage 2
- `3` : Stage 3
- `4` : Stage 4

実装参照: [Assets/Scripts/Pose/PoseDebugOverlay.cs](Assets/Scripts/Pose/PoseDebugOverlay.cs), [Assets/Scripts/Stages/StageSequenceController.cs](Assets/Scripts/Stages/StageSequenceController.cs)

## 使い始め方

このリポジトリには `.unity` シーンファイルが含まれていないため、既存の作業シーンを使うか、新しくシーンを作成してセットアップしてください。

基本手順:

1. Unity 6000.0.68f1 でプロジェクトを開く
2. 任意のシーンを開く、または新規シーンを作る
3. 空の GameObject を 1 つ作り、PoseTestBootstrap を付ける
4. 空の GameObject を 1 つ作り、StageSequenceController を付ける
5. StageSequenceController の Inspector から `Create Missing Stage Setup` または `Sync Stage Setup` を実行する
6. Play Mode に入り、stage2 が初期表示されることを確認する

### stage roots を自前で使いたい場合

StageSequenceController の `stageRoots` には次の 2 通りで設定できます。

- Scene 上の GameObject を直接割り当てる
- Prefab を割り当てて、builder 側にシーン内インスタンスを生成させる

現在の実装では、手動で割り当てた参照を消さないようにしてあります。Prefab を入れた場合も、アセットを直接壊さず、シーン内に生成した root を使う設計です。

## Edit Mode と Play Mode の違い

### Edit Mode

- 配置確認しやすいようにプレビューを生成する
- 必要に応じて、完全な黒背景化は避ける設定が使える

### Play Mode

- 投影リグを再構築する
- 黒背景とスポットライト主体の見え方を適用する
- ActiveSpotLight をステージ側が参照してギミックを動かす

## Inspector からよく触る場所

### PoseTestBootstrap

- `buildOnStart`: 再生開始時に自動ビルドするか
- `buildPreviewInEditMode`: Edit Mode でプレビューを出すか
- `forceBlackEnvironment`: 実行時に背景を黒く寄せるか
- `disableOtherSceneLights`: 実行時に他ライトを無効化するか
- `startInFullscreen`: 起動時にフルスクリーンにするか
- `fullscreenToggleKey`: フルスクリーントグルキー（既定: F11）

### StageSequenceController

- `stageRoots`: 各ステージの root（4 要素）
- `startingStageIndex`: 開始時ステージ。現在の既定値は Stage 2（index = 1）
- `autoCreateStageSetupIfMissing`: 足りない構成を自動生成するか
- `autoSyncStageSetup`: 編集時に自動同期するか

Editor 拡張のボタン:

- `Sync Stage Setup`
- `Create Missing Stage Setup`

実装参照: [Assets/Editor/StageSequenceControllerEditor.cs](Assets/Editor/StageSequenceControllerEditor.cs)

## 既知の注意点

- このリポジトリには `.unity` シーンが入っていないため、シーン構成は利用者側で用意する必要があります
- Stage 2 は実装変更が多かった箇所で、見た目の最終仕様はまだ流動的です
- 自動同期が有効な場合、stage root 配下に builder が不足構成を補完します
- `Assets/Stage1Manager.cs` はクリック操作ベースのレガシー版 Stage 1 実装です。現行の `StageLightOrderedPuzzle` とは別物であり、通常は使用しません

## 参照ドキュメント

- [ARCHITECTURE.md](ARCHITECTURE.md) - コンポーネント構成図と依存関係
- [CONVERSATION_SUMMARY.md](CONVERSATION_SUMMARY.md) - 開発経緯と現在の確定仕様

## 主要ファイル一覧

### Pose 系

| ファイル | 役割 |
|---|---|
| [PoseTestBootstrap.cs](Assets/Scripts/Pose/PoseTestBootstrap.cs) | 投影リグ生成・ActiveSpotLight 公開 |
| [UdpQuaternionReceiver.cs](Assets/Scripts/Pose/UdpQuaternionReceiver.cs) | ZIG SIM からの UDP/OSC クォータニオン受信 |
| [PoseRotationDriver.cs](Assets/Scripts/Pose/PoseRotationDriver.cs) | 受信クォータニオンをスポットライト回転に反映 |
| [PoseCalibrationCoordinator.cs](Assets/Scripts/Pose/PoseCalibrationCoordinator.cs) | キャリブレーション調整 |
| [PoseDebugOverlay.cs](Assets/Scripts/Pose/PoseDebugOverlay.cs) | デバッグ UI・D キー/C キー処理 |
| [TestScreenVisualizer.cs](Assets/Scripts/Pose/TestScreenVisualizer.cs) | 投影面とポインタの可視化 |
| [ProjectionSurface.cs](Assets/Scripts/Pose/ProjectionSurface.cs) | 投影面サーフェスの定義 |
| [OffAxisProjectionCamera.cs](Assets/Scripts/Pose/OffAxisProjectionCamera.cs) | 非軸投影カメラ行列の計算 |
| [QuaternionCoordinateConverter.cs](Assets/Scripts/Pose/QuaternionCoordinateConverter.cs) | 座標系変換（iOS → Unity） |

### Stage 系（共通）

| ファイル | 役割 |
|---|---|
| [StageSequenceController.cs](Assets/Scripts/Stages/StageSequenceController.cs) | Stage 1〜4 の切り替え管理 |
| [StageSequenceDebugBuilder.cs](Assets/Scripts/Stages/StageSequenceDebugBuilder.cs) | デバッグ用ステージ構成の自動生成 |
| [StageRootMarker.cs](Assets/Scripts/Stages/StageRootMarker.cs) | stage root 識別マーカー |
| [SpotlightSensor.cs](Assets/Scripts/Stages/SpotlightSensor.cs) | ライト照射判定（IsLit / Exposure01） |
| [StageSpotlightSettings.cs](Assets/Scripts/Stages/StageSpotlightSettings.cs) | ステージごとのスポットライト設定 |
| [StageSpotlightMaterialUtility.cs](Assets/Scripts/Stages/StageSpotlightMaterialUtility.cs) | スポットライト用マテリアルユーティリティ |
| [StageTransitionFader.cs](Assets/Scripts/Stages/StageTransitionFader.cs) | ステージ遷移フェード演出 |
| [FaceCameraBillboard.cs](Assets/Scripts/Stages/FaceCameraBillboard.cs) | カメラ方向を向くビルボード |
| [LightReactiveRendererFeedback.cs](Assets/Scripts/Stages/LightReactiveRendererFeedback.cs) | ライト照射に応じたレンダラー色変化 |
| [LightReactiveLineFeedback.cs](Assets/Scripts/Stages/LightReactiveLineFeedback.cs) | ライト照射に応じたライン色変化 |
| [LightReactiveTextFeedback.cs](Assets/Scripts/Stages/LightReactiveTextFeedback.cs) | ライト照射に応じたテキスト色変化 |

### Stage 1

| ファイル | 役割 |
|---|---|
| [StageLightOrderedPuzzle.cs](Assets/Scripts/Stages/StageLightOrderedPuzzle.cs) | 順番照射パズル制御 |
| [StageLightCreatureTarget.cs](Assets/Scripts/Stages/StageLightCreatureTarget.cs) | 個別ターゲット（Hide/Hop リアクション） |

### Stage 2

| ファイル | 役割 |
|---|---|
| [StageSymbolNumberRevealPuzzle.cs](Assets/Scripts/Stages/StageSymbolNumberRevealPuzzle.cs) | 前半: 記号解読パズル |
| [StageSymbolNumberRevealTarget.cs](Assets/Scripts/Stages/StageSymbolNumberRevealTarget.cs) | 前半: 個別解読ターゲット |
| [StageSymbolMappingDisplay.cs](Assets/Scripts/Stages/StageSymbolMappingDisplay.cs) | 前半: 記号マッピングテキスト表示 |
| [StageLightCodeLockPuzzle.cs](Assets/Scripts/Stages/StageLightCodeLockPuzzle.cs) | 後半: コードロックパズル（正解 `834`） |
| [StageLightCodeDialColumn.cs](Assets/Scripts/Stages/StageLightCodeDialColumn.cs) | 後半: 桁ダイヤル制御 |
| [StageLightCodeDigitAnimator.cs](Assets/Scripts/Stages/StageLightCodeDigitAnimator.cs) | 後半: 桁数字のアニメーション |
| [StageCodeFormulaDisplay.cs](Assets/Scripts/Stages/StageCodeFormulaDisplay.cs) | 後半: 式ラベル表示制御 |
| [StageCodeLockButtonIndicator.cs](Assets/Scripts/Stages/StageCodeLockButtonIndicator.cs) | 後半: ボタン照射インジケーター |
| [StageCodeLockRig.cs](Assets/Scripts/Stages/StageCodeLockRig.cs) | 後半: コードロックリグ全体 |
| [Stage2PuzzleController.cs](Assets/Scripts/Stages/Stage2PuzzleController.cs) | Stage 2 全体の進行調停 |
| [Stage2CompletionSequence.cs](Assets/Scripts/Stages/Stage2CompletionSequence.cs) | Stage 2 完了演出（崩落・ライト拡張・自走） |

### Stage 3 / Stage 4

| ファイル | 役割 |
|---|---|
| [Stage3RockHintPuzzle.cs](Assets/Scripts/Stages/Stage3RockHintPuzzle.cs) | Stage 3/4 共通: カラーヒントロックパズル |
