# yugo_ARD-ShibaLab

このプロジェクトは、Unity 上で動くライト照射型のインタラクション試作環境です。

大きく 2 つの要素で構成されています。

- 姿勢入力とスポットライト投影を扱う Pose / Projection 系
- スポットライト照射で進行する Stage 1 / Stage 2 / Stage 3 のギミック系

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
- Stage 1, Stage 2, Stage 3 を切り替えて、ライト照射ギミックを検証する
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

このスクリプトが生成したスポットライトは `ActiveSpotLight` として公開され、ステージ側の [Assets/Scripts/Stages/SpotlightSensor.cs](Assets/Scripts/Stages/SpotlightSensor.cs) から参照されます。

関連する代表スクリプト:

- [Assets/Scripts/Pose/PoseTestBootstrap.cs](Assets/Scripts/Pose/PoseTestBootstrap.cs)
- [Assets/Scripts/Pose/PoseDebugOverlay.cs](Assets/Scripts/Pose/PoseDebugOverlay.cs)
- UdpQuaternionReceiver
- PoseRotationDriver
- TestScreenVisualizer
- ProjectionSurface
- OffAxisProjectionCamera

### ステージ系

ステージ切り替えの中心は [Assets/Scripts/Stages/StageSequenceController.cs](Assets/Scripts/Stages/StageSequenceController.cs) です。

このコンポーネントは以下を担当します。

- Stage 1 / 2 / 3 の root 管理
- 初期表示ステージの切り替え
- キーボードによるステージ移動
- 現在アクティブなステージ配下のライト反応処理の再評価
- 編集時のステージ自動同期

ステージ内容の自動生成は [Assets/Scripts/Stages/StageSequenceDebugBuilder.cs](Assets/Scripts/Stages/StageSequenceDebugBuilder.cs) が担当します。

この builder は以下の用途で使います。

- stage root が足りないときの自動生成
- Stage 1 / 2 / 3 のデバッグ用初期配置作成
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

Stage 2 は「記号と数字の対応をライトで見つける」ギミックです。

- 実装: [Assets/Scripts/Stages/StageSymbolNumberRevealPuzzle.cs](Assets/Scripts/Stages/StageSymbolNumberRevealPuzzle.cs)
- 個別ターゲット: [Assets/Scripts/Stages/StageSymbolNumberRevealTarget.cs](Assets/Scripts/Stages/StageSymbolNumberRevealTarget.cs)
- 表示制御: [Assets/Scripts/Stages/StageSymbolMappingDisplay.cs](Assets/Scripts/Stages/StageSymbolMappingDisplay.cs)

現在の Stage 2 は次のように動きます。

- 各 Stage2 Object は見えないホスト GameObject として存在する
- その子に Mapping Display が自動生成され、TextMesh で `□=4`, `△=3`, `○=8` を表示する
- SpotlightSensor の `Exposure01` を使って文字の明るさと表示状態を更新する
- 3 つすべてにライトを当てると完了マーカーを有効化する

補足:

- Stage 2 のホストには、以前のような板オブジェクトは生成しません
- 以前の案内ラベル `Stage 2 / Light the symbol-number objects` も自動削除されます
-文字の表示はprefab2\Stage2 Object 1 ~ 3で管理しています。

### Stage 3

Stage 3 は「ライトで 3 桁コードを操作してドアを開く」ギミックです。

- 実装: [Assets/Scripts/Stages/StageLightCodeLockPuzzle.cs](Assets/Scripts/Stages/StageLightCodeLockPuzzle.cs)
- 桁操作: [Assets/Scripts/Stages/StageLightCodeDialColumn.cs](Assets/Scripts/Stages/StageLightCodeDialColumn.cs)

仕組み:

- 各列の上下ボタンにライトを当てると数字が増減する
- 現在の正解コードは `834`
- 正解すると式ラベルの色が変わり、ドアが上方向へ開く

## ライト判定の仕組み

ライト照射判定は [Assets/Scripts/Stages/SpotlightSensor.cs](Assets/Scripts/Stages/SpotlightSensor.cs) で共通化しています。

主な役割:

- PoseTestBootstrap が生成した `ActiveSpotLight` を解決する
- サンプル位置に対して距離と角度から露出量を計算する
- `IsLit` と `Exposure01` を毎フレーム更新する
- 必要なら line of sight 判定も行う

Stage 2 の文字発光も、Stage 1 の進行判定も、Stage 3 の上下ボタン操作も、このセンサーを基準にしています。

## 操作方法

### 共通キー

- `D`: デバッグオーバーレイの表示切り替え
- `C`: キャリブレーションや表示基準のリセット

### ステージ切り替えキー

- `[` : 前のステージ
- `]` : 次のステージ
- `1` : Stage 1
- `2` : Stage 2
- `3` : Stage 3

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

### StageSequenceController

- `stageRoots`: 各ステージの root
- `startingStageIndex`: 開始時ステージ。現在の既定値は Stage 2
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
- Stage 3 の見た目はデモ向けに調整済みですが、必要ならレイアウト再調整の余地があります

## 参照ドキュメント

- [CONVERSATION_SUMMARY.md](CONVERSATION_SUMMARY.md)
- [GITHUB_COLLABORATION_GUIDE.md](GITHUB_COLLABORATION_GUIDE.md)

## 主要ファイル一覧

- [Assets/Scripts/Pose/PoseTestBootstrap.cs](Assets/Scripts/Pose/PoseTestBootstrap.cs)
- [Assets/Scripts/Pose/PoseDebugOverlay.cs](Assets/Scripts/Pose/PoseDebugOverlay.cs)
- [Assets/Scripts/Stages/StageSequenceController.cs](Assets/Scripts/Stages/StageSequenceController.cs)
- [Assets/Scripts/Stages/StageSequenceDebugBuilder.cs](Assets/Scripts/Stages/StageSequenceDebugBuilder.cs)
- [Assets/Scripts/Stages/SpotlightSensor.cs](Assets/Scripts/Stages/SpotlightSensor.cs)
- [Assets/Scripts/Stages/StageLightOrderedPuzzle.cs](Assets/Scripts/Stages/StageLightOrderedPuzzle.cs)
- [Assets/Scripts/Stages/StageSymbolMappingDisplay.cs](Assets/Scripts/Stages/StageSymbolMappingDisplay.cs)
- [Assets/Scripts/Stages/StageLightCodeLockPuzzle.cs](Assets/Scripts/Stages/StageLightCodeLockPuzzle.cs)
