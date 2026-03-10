# 会話要約
**この履歴を参照し、実装するように。**
## 目的の変遷

この作業は、当初は投影・姿勢トラッキングの検証環境を Unity 上で整えることから始まり、その後、デモ用のステージギミック実装へ重点が移った。

前半では以下が主な目的だった。

- 二面投影リグの見た目と制御の整理
- 実行時に自動で構築される検証環境の安定化
- ZIG SIM 由来の入力でリセンターできるようにする
- デバッグ表示を使いやすくする
- Play Mode 以外でも Scene 上で配置調整できるようにする

後半では以下に主眼が移った。

- Stage 1, 2, 3 を切り替えられるステージ基盤の追加
- ライト照射を使ったギミックの実装
- stage root が存在しない場合でもデバッグ用オブジェクトを自動生成する仕組みの追加
- Stage 2 の文字表示とライト反応の安定化
- デモ向けに stage2 をデフォルト表示にする調整

## 実装済みの主要変更

### 1. Pose / Projection 系

- 壁や面の見え方を整理し、二面投影リグを扱いやすくした
- PoseTestBootstrap を中心に、必要コンポーネントをホスト側へ集約した
- 実行時に自動ビルドされる挙動を整理した
- ActiveSpotLight を外部から参照できるようにした
- ZIG SIM のタッチ入力でリセンターできるようにした
- 実行時は黒背景に寄せ、ライトが当たっている箇所だけ見える方向へ調整した
- Edit Mode では真っ暗になりすぎないようにし、Scene 上での配置作業をしやすくした

### 2. デバッグ UI

- デバッグ表示のトグルを D キーで行えるようにした
- C キーでリセット操作を呼べるようにした
- 実行時生成オブジェクト側にだけ付いていたコンポーネントを見直し、Inspector から調整しやすい構成へ変更した
- OnGUI 経由でキー入力を拾うようにし、ExecuteAlways 下でも反応しやすくした

### 3. ステージ基盤

- StageSequenceController を追加し、単一シーン内で Stage 1, Stage 2, Stage 3 を切り替えられるようにした
- StageRootMarker を追加し、stage root を後から再発見できるようにした
- StageSequenceDebugBuilder を追加し、足りないステージ構成を自動生成できるようにした
- 破壊的に作り直すのではなく、既存配置をなるべく維持する方向へ調整した
- Editor 拡張から Create Missing Stage Setup / Sync Stage Setup を実行できるようにした

### 4. Stage 1

- SpotlightSensor を使って、順番にライトを当てるギミックを実装した
- StageLightCreatureTarget と StageLightOrderedPuzzle を追加した
- 正しい順で照らしたときのみ進行するステージとして動作する構成にした

### 5. Stage 2

Stage 2 はこの会話の中で最も試行錯誤が多かった箇所で、以下の変遷があった。

- 最初は記号と数字を個別に出す方向で構成した
- その後、文字列として □=4, △=3, ○=8 を見せる方針に変更した
- Billboard 化、疑似セグメント表示、TextMeshPro 利用、built-in TextMesh 利用など複数の方式を試した
- 旧生成物が Scene 内に残って表示を壊す問題があり、レガシー掃除処理を加えた
- TMP 依存で例外が出たため、built-in TextMesh に寄せた
- さらに Unity の内蔵フォント指定が Arial.ttf では無効になっていたため、LegacyRuntime.ttf に修正した

現時点で Stage 2 は以下の構成になっている。

- StageSymbolMappingDisplay が 1 つの TextMesh を管理する
- SpotlightSensor の Exposure01 を使って、文字の色と表示状態を更新する
- 未照射時は透明寄り、照射時は litTextColor に近づく実装になっている
- StageSymbolNumberRevealTarget は、ライトが当たった履歴を管理する
- StageSymbolNumberRevealPuzzle は、3 つすべて見つけたら完了扱いにする

ただし、Stage 2 は会話上で何度も仕様修正と実装修正が入っており、最終見た目が完全に確定したとは言い切れない。特に「光っている見え方」の期待値については、ユーザーから再度指摘が入っている。

### 6. Stage 3

- 3 桁のコードロックをライトで操作するギミックを実装した
- StageLightCodeDialColumn と StageLightCodeLockPuzzle を追加した
- 上下ボタンをライトで照らして数字を進める構成にした
- デモ向けに、記号や数字表示の位置とサイズを再調整した

## 主な問題と対応

### デバッグ表示が反応しない

- D キーで消せない、表示されないという問題があった
- キー入力の拾い方を見直し、OnGUI 経由の処理へ調整した

### Inspector から編集できない

- 実行時に別オブジェクトへコンポーネントが生え、元のホストから操作できない状態だった
- Bootstrap 側へ寄せる構成に変更した

### Scene 編集時に暗すぎる

- 実行時の黒背景・スポットライトのみ表示という要件と、Scene 編集時の見やすさが競合した
- Edit Mode と Play Mode の見え方を分けて調整した

### Stage 2 の表示が壊れる

- 古い生成物が残る
- 文字の向きが反転する
- 斜め配置で不自然な表示になる
- ライトを当てても変化しない
- 謎の棒だけが大量に見える
- TMP 初期設定依存で例外が出る

これらに対し、以下を実施した。

- FaceCameraBillboard の向きを修正した
- StageRootMarker で既存 root を再利用できるようにした
- Stage 2 の旧生成物を掃除する処理を加えた
- 表示方式を built-in TextMesh に簡略化した
- フォント名を LegacyRuntime.ttf に修正した
- 照射量を Color に反映する形へ戻した

## 現在の状態

### 安定しているもの

- Pose / Projection の基本構成
- Debug UI のトグル
- Edit Mode / Play Mode での見え方の分離
- Stage 基盤の切り替え
- Stage 1 の基本ギミック
- Stage 3 のコードロック基盤

### 注意が必要なもの

- Stage 2 の最終的な「光り方」の見え方
- Scene 内に既存オブジェクトが残っている場合の見た目差分
- デモ時に stage2 を初期表示へ寄せるための既存シーン反映状況

## デモ向けの直近変更

- StageSequenceController で stage2 がデフォルト開始になるようにした
- StageSymbolMappingDisplay の built-in font を LegacyRuntime.ttf に変更した
- Stage 3 側の記号・数字表示の位置とサイズを再調整した
- Stage 2 の文字表示は、SpotlightSensor の Exposure01 を使って文字色に反映する形へ戻した

## 関連する主要ファイル

- Assets/Scripts/Pose/PoseTestBootstrap.cs
- Assets/Scripts/Pose/PoseDebugOverlay.cs
- Assets/Scripts/Stages/StageSequenceController.cs
- Assets/Scripts/Stages/StageSequenceDebugBuilder.cs
- Assets/Scripts/Stages/StageRootMarker.cs
- Assets/Scripts/Stages/SpotlightSensor.cs
- Assets/Scripts/Stages/StageLightCreatureTarget.cs
- Assets/Scripts/Stages/StageLightOrderedPuzzle.cs
- Assets/Scripts/Stages/StageSymbolMappingDisplay.cs
- Assets/Scripts/Stages/StageSymbolNumberRevealTarget.cs
- Assets/Scripts/Stages/StageSymbolNumberRevealPuzzle.cs
- Assets/Scripts/Stages/StageLightCodeDialColumn.cs
- Assets/Scripts/Stages/StageLightCodeLockPuzzle.cs
- Assets/Scripts/Stages/FaceCameraBillboard.cs
- Assets/Editor/StageSequenceControllerEditor.cs

## 今後の確認ポイント

1. Stage 2 の文字が、ユーザーの期待する「光る見え方」になっているかを実機で確認する
2. 既存 Scene に古い Stage 2 生成物が残っていないかを確認する
3. デモ前に stage2 が確実に初期表示されるかを確認する
4. 必要であれば Stage 3 の数字や記号にも、照射量ベースの視覚フィードバックを入れる