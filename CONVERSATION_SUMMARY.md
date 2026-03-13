# 開発経緯と確定仕様

## プロジェクトの目的

Unity 上でライト照射型インタラクションを検証するデモ環境。  
ZIG SIM（iOS）からの姿勢入力を UDP/OSC で受け取り、スポットライトで Stage を操作する。

---

## 確定済みの仕様

### Pose / Projection 系

- `PoseTestBootstrap` が実行時にリグを自動構築し、`ActiveSpotLight` を公開する
- `UdpQuaternionReceiver` が別スレッドで UDP 受信し、クォータニオンを変換する
- ZIG SIM のタッチ入力によるリセンターに対応している
- Play Mode は黒背景＋スポットライトのみ、Edit Mode はプレビュー表示
- シェーダーグローバル変数 (`_StageSpotlightPosition` 等) を毎フレーム更新している
- フルスクリーン切り替え: F11 または `fullscreenToggleKey`

### ステージ基盤

- `StageSequenceController` が Stage 1〜4 を管理する（`stageRoots` 配列 = 4 要素）
- `StageSequenceDebugBuilder` がステージ自動生成を担う
- デフォルト開始ステージは Stage 2（`startingStageIndex = 1`）
- キー操作: `[`/`]` で前後、`1`〜`4` で直接ジャンプ

### Stage 1: 順番照射パズル

- `StageLightOrderedPuzzle` + `StageLightCreatureTarget` で構成
- Creature 3 体を指定順で照射 → 全完了で `IsSolved = true`
- 間違い照射時はリセット（`resetOnWrongTarget = true`）

### Stage 2: 記号解読 → コードロック

- **前半**: `□=4`, `△=3`, `○=8` の 3 オブジェクトを照射すると解読完了
- **後半**: 3 桁ダイヤルを操作し `834` を入力 → ドアが開く
- **完了演出**: パネル崩落 → ライト拡張 → stage root 自走 → Stage 3 遷移
- テキストは built-in TextMesh（`LegacyRuntime.ttf`）を使用（TMP は使わない）

### Stage 3 / Stage 4: カラーヒントロックパズル

- 共に `Stage3RockHintPuzzle` を使用
- 緑・青のヒント岩をライト中心で一定時間照射するとフィナーレへ
- Stage 3 完了後 → Stage 4 へ自動遷移
- Stage 4 は最終ステージ（遷移なし）

### ライト判定

- `SpotlightSensor` が `IsLit` / `Exposure01` (0〜1) を毎フレーム計算
- `ActiveSpotLight` は `FindFirstObjectByType<PoseTestBootstrap>()` で自動解決
- line of sight 判定オプション (`requireLineOfSight`) あり

---

## 注意事項

- `.unity` シーンファイルはリポジトリに含まれていない
- `Assets/Stage1Manager.cs` はレガシー版（クリック操作ベース）であり、現行とは無関係
- `Stage2CompletionSequence` の完了演出パラメーターは Inspector で調整可能
- Stage 2 の「光り方の見え方」は今後さらに調整の余地あり