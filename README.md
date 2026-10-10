# Plugin Chat Overlay

プラグイン通知をタブ分け可能な専用オーバーレイウィンドウに分離します。分離は初期OFFです。

## 導入

1. Dalamud設定のカスタムプラグインリポジトリに、次のURLを追加します。
2. プラグイン一覧から **Plugin Chat Overlay** をインストールします。
3. `/pcoverlay config` →「基本」→「プラグイン通知分離」を有効にします。

```text
https://raw.githubusercontent.com/minasis-min/DalamudPlugins/main/pluginmaster.json
```

## 主な機能

- プラグイン別のタブ振り分け、タブの独立ウィンドウ化・統合。
- 元のチャット色を保持し、時刻の色・フォント・文字サイズ・行間・背景透明度を調整。
- ドラッグ選択とCtrl+C、右クリックでログをコピー。Ctrl+Cコピーは設定でOFFにできます。
- ウィンドウの移動・サイズ変更、スナップ、タイトル・タブの非表示。
- アイテムリンクのネイティブツールチップ、カットシーン・ログアウト中の非表示。

UIは **Dalamud標準／MinasisUI** を選択できます（既定：Dalamud標準）。

## コマンド

| コマンド | 動作 |
| --- | --- |
| `/pcoverlay` | チャットウィンドウを表示 |
| `/pcoverlay config` | 設定を開く |
| `/pcoverlay reset` | ウィンドウの位置・サイズを戻す |
| `/pcoverlay test` | テスト通知を出力 |

## 補足

- 対応環境：Windows x64 / Dalamud 15.0.3.6（API 15）。ゲーム・Dalamudの更新で分離が停止する場合があります。
- 一部の出力方式や他プラグインが先に非表示にした通知は捕捉できません。タブ振り分けは先頭の `[名前]`・`名前:` を認識し、名前のない通知は未分類になります。
- 他プラグインに通知を非表示にする設定がある場合は、必要に応じてその設定をOFFにしてください。
- 履歴は既定100件、最大1,000件。終了・再ロードで消えます。設定はDalamud標準の設定フォルダへ保存します。
- 不具合は [Issues](https://github.com/minasis-min/PluginChatOverlay/issues) へ版・再現手順を添えて報告してください。対応可能な範囲で確認します。

## ライセンス

ライセンス：[MIT](LICENSE)
