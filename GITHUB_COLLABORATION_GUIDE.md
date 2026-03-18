# GitHub共同編集ガイド

## このプロジェクトで追跡するもの
Git で管理する主なフォルダは次です。

- `Assets/`
- `Packages/`
- `ProjectSettings/`

必要に応じて、設定ファイルとして次も追跡できます。

- `.vscode/settings.json`
- `.vscode/extensions.json`
- `.vscode/tasks.json`
- `.gitignore`
- `.gitattributes`

## Git で管理しないもの
Unity が自動生成するため、毎回コミットしないものです。

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `*.csproj`
- `*.sln`
- `*.slnx`

## GitHub共同編集のおすすめ手順

### 1. 最初にやること
1. GitHub で空のリポジトリを作る
2. ローカルのこのフォルダを Git 管理にする
3. 最初のコミットを作る
4. GitHub に push する

## ブランチ運用のおすすめ
初心者向けには次の運用が安全です。

- `main`: 動いている安定版
- 作業ごとにブランチを切る
  - 例: `feature/udp-fix`
  - 例: `feature/projection-tuning`
  - 例: `fix/light-boundary`

### 共同編集の基本
- `main` に直接コミットしない
- 1つの作業ごとにブランチを作る
- 作業が終わったら Pull Request を作る
- レビュー後に `main` へマージする

## Unity プロジェクトで特に大事な設定

### 1. Meta ファイルを必ず有効にする
Unity の設定で **Visible Meta Files** を有効にしてください。

- `Edit > Project Settings > Editor > Version Control > Mode = Visible Meta Files`

### 2. Asset Serialization を Force Text にする
Prefab や Scene の差分を見やすくするため、次を有効にしてください。

- `Edit > Project Settings > Editor > Asset Serialization > Mode = Force Text`

### 3. Scene / Prefab は同時編集を避ける
Unity では次のファイルが競合しやすいです。

- `*.unity`
- `*.prefab`
- `*.mat`
- `*.asset`

同じ Scene を複数人で同時に触ると競合しやすいので、担当を分けてください。

## コミット前チェック
コミット前に次を確認してください。

- Unity でエラーが出ていない
- Scene が意図せず変更されていない
- 不要なファイルがステージされていない
- `Library/` や `Temp/` が含まれていない

## コミットメッセージ例
短くて分かりやすい英語で十分です。

- `Add UDP quaternion receiver`
- `Fix projector boundary mapping`
- `Adjust left display camera`
- `Update pose debug overlay`

## 競合を減らすコツ
- 同じ Scene を同時に編集しない
- Prefab 化できるものは Prefab に分ける
- 1回の変更を小さくする
- こまめに pull する
- 大きい変更の前にチームで声をかける

## Git LFS を使ったほうがよいもの
次のような大きいバイナリをたくさん扱うなら Git LFS を検討してください。

- 動画
- 高解像度画像
- 音声
- 3Dモデル

## 初心者向けの最低限の流れ
1. 最新の `main` を pull する
2. 新しいブランチを作る
3. Unity で作業する
4. 変更を確認する
5. コミットする
6. GitHub に push する
7. Pull Request を作る

## 迷ったときに見ればよいもの
- Git に入れる: `Assets/`, `Packages/`, `ProjectSettings/`
- Git に入れない: `Library/`, `Temp/`, `Logs/`, `UserSettings/`
- 競合しやすい: Scene, Prefab, Material, Asset
- 安全策: ブランチを分ける / 小さくコミットする / 早めに共有する
