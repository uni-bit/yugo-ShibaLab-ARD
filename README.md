<<<<<<< HEAD
# test_kakudo

Unity で UDP 経由の姿勢クォータニオンを受信し、3Dオブジェクトとスポットライト、2面投影用カメラを制御するプロジェクトです。

## 主な機能
- UDP 受信で `Quaternion(x, y, z, w)` を取得
- iPhone / Core Motion 系の姿勢データを Unity 用に変換
- 3Dモデルと先端ライトを連動
- 前面 + 左面の L 字投影に対応
- 2台目表示用カメラ (`Display 2`) に対応
- デバッグ表示あり

## 主要フォルダ
- `Assets/Scripts/Pose/` : 姿勢受信・投影・ライト制御スクリプト
- `Assets/Editor/` : Inspector 拡張
- `Packages/` : Unity パッケージ設定
- `ProjectSettings/` : Unity プロジェクト設定

## 主なスクリプト
- `UdpQuaternionReceiver` : UDP 受信
- `QuaternionCoordinateConverter` : 座標変換
- `PoseRotationDriver` : モデル回転 + ライト位置制御
- `TestScreenVisualizer` : ライト照準制御
- `ProjectionSurface` : 投影面定義
- `OffAxisProjectionCamera` : オフアクシス投影カメラ
- `PoseDebugOverlay` : デバッグ表示
- `PoseTestBootstrap` : デモ構成生成

## Unity 側の推奨設定
Unity で次を有効にしてください。

- `Edit > Project Settings > Editor > Version Control > Mode = Visible Meta Files`
- `Edit > Project Settings > Editor > Asset Serialization > Mode = Force Text`

## Git で管理するもの
- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `.gitignore`
- `.gitattributes`

## Git で管理しないもの
- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `*.csproj`
- `*.sln`
- `*.slnx`

## 共同編集の基本
- `main` に直接作業しない
- 作業ごとにブランチを作る
- 変更が終わったら Pull Request を作る
- Scene / Prefab の同時編集は避ける

詳しくは [GITHUB_COLLABORATION_GUIDE.md](GITHUB_COLLABORATION_GUIDE.md) を参照してください。

## VS Code だけで GitHub 共同編集する流れ
### 最初の1回
1. VS Code でこのフォルダを開く
2. 左のソース管理を開く
3. `リポジトリを初期化` を押す
4. 変更一覧にファイルが出ることを確認する
5. メッセージ欄に `Initial commit` と入力してコミットする
6. 左下やソース管理の案内から GitHub に公開する
7. リポジトリ名を決めて公開する

### ふだんの作業
1. ソース管理を開く
2. `...` メニューやブランチ表示から新しいブランチを作る
3. Unity で作業する
4. VS Code に戻って差分を確認する
5. コミットメッセージを書いてコミットする
6. `同期` または `Publish Branch` を押して GitHub に送る
7. GitHub 上で Pull Request を作る

### 共同編集者を招待する
1. GitHub のリポジトリページを開く
2. `Settings` > `Collaborators` を開く
3. 相手の GitHub アカウントを招待する

## VS Code で気をつけること
- Unity を開いたままでもよいが、コミット前にエラー確認をする
- 意図せず変更された Scene や Prefab がないか毎回確認する
- `Library/` などが含まれていないか確認する
- コミットは小さく分ける

## 困ったとき
- 大量の差分が出たら `Library/` などが混ざっていないか確認
- マージ競合が出たら Scene / Prefab を同時編集していないか確認
- 動かなくなったら最新の `main` と差分を見直す
=======
# yugo-ShibaLab-ARD
>>>>>>> 398ddfb877fdf5fa42041068f80d4de9686b8402
