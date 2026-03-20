# Yobidashi（よびだし）

日本語ユーザー向けのテキスト展開（Text Expander）Windowsデスクトップアプリ

## 概要

Yobidashi は、Google日本語入力やMS-IMEと併用しながら、ユーザーが登録したトリガー文字列を入力するだけで、定型文やテンプレートをその場で展開できる Windows 常駐アプリケーションです。

**日本語トリガーに対応**しており、「じゅうしょ」「しょめい」「へんしん」などの日本語の呼び出し語で自然に定型文を呼び出せます。

## 特徴

- **日本語トリガー対応** - ひらがな、カタカナ、漢字まじりのトリガー文字列で展開
- **IME共存** - 変換中の未確定文字列には反応しない安全設計
- **長文展開** - 署名、メール文、案内文など1000文字以上の長文テンプレートに対応
- **検索ポップアップ** - `Ctrl+Space` でどこからでも定型文を検索・挿入
- **変数展開** - `{{date}}` `{{time}}` `{{clipboard}}` `{{cursor}}` などのプレースホルダ
- **完全ローカル** - ネットワーク不要、データはすべてローカルSQLiteに保存
- **日本語UI** - 最初から日本語で設計されたインターフェース

## スクリーンショット

（開発中）

## 動作環境

- Windows 10 19041 以降（Windows 11 推奨）
- .NET 8 Runtime
- Windows App SDK 1.5

## ビルド手順

### 前提条件

1. [Visual Studio 2022](https://visualstudio.microsoft.com/) (17.8以降)
   - ワークロード: 「.NET デスクトップ開発」
   - ワークロード: 「Windows アプリケーション開発」
2. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
3. [Windows App SDK 1.5](https://learn.microsoft.com/windows/apps/windows-app-sdk/)

### ビルド

```bash
# リポジトリをクローン
git clone https://github.com/naofumi0814/Yobidashi.git
cd Yobidashi

# ビルド
dotnet build src/Yobidashi/Yobidashi.csproj -c Release -r win-x64

# 実行
dotnet run --project src/Yobidashi/Yobidashi.csproj -c Release -r win-x64
```

または Visual Studio でソリューションファイル `Yobidashi.sln` を開いてビルドしてください。

### 発行（配布用ビルド）

```bash
dotnet publish src/Yobidashi/Yobidashi.csproj -c Release -r win-x64 --self-contained
```

## 使い方

### 基本的な使い方

1. アプリを起動するとシステムトレイに常駐します
2. メイン画面で定型文を登録します（トリガー文字列と本文を設定）
3. 任意のアプリケーションでトリガー文字列を入力し、Space または Tab を押すと本文が展開されます

### 例

| トリガー | 展開内容 |
|----------|----------|
| `じゅうしょ` + Space | 住所全文 |
| `しょめい` + Space | 署名ブロック |
| `へんしん` + Space | 返信テンプレート |
| `おれい` + Space | お礼文 |
| `きょう` + Space | 今日の日付 |

### 検索ポップアップ

トリガー文字列を覚えていない場合は、`Ctrl+Space` で検索ポップアップを呼び出して、キーワード検索から定型文を選んで挿入できます。

### 変数

本文中で以下の変数が使えます:

| 変数 | 展開結果 |
|------|----------|
| `{{date}}` | 今日の日付 (2024/01/15) |
| `{{time}}` | 現在時刻 (14:30) |
| `{{date_jp}}` | 今日の日付 (2024年01月15日) |
| `{{weekday}}` | 曜日 (月) |
| `{{clipboard}}` | クリップボードの内容 |
| `{{cursor}}` | 展開後のカーソル位置 |

## プロジェクト構成

```
Yobidashi/
├── Yobidashi.sln              # ソリューションファイル
├── DESIGN.md                  # 設計ドキュメント
├── README.md                  # このファイル
└── src/
    └── Yobidashi/
        ├── Yobidashi.csproj   # プロジェクトファイル
        ├── App.xaml(.cs)      # アプリケーション起動
        ├── Program.cs         # エントリポイント
        ├── Core/              # コアロジック
        │   ├── Expansion/     # テキスト展開エンジン
        │   │   ├── TextExpander.cs
        │   │   └── VariableProcessor.cs
        │   ├── Hooks/         # キーボードフック
        │   │   └── GlobalKeyboardHook.cs
        │   └── Services/      # サービス層
        │       ├── AutoStartManager.cs
        │       ├── ExpansionService.cs
        │       ├── ExportImportService.cs
        │       ├── HotkeyManager.cs
        │       ├── ImeStateDetector.cs
        │       ├── TextNormalizer.cs
        │       └── TriggerDetector.cs
        ├── Data/              # データアクセス
        │   ├── DatabaseManager.cs
        │   ├── Models/
        │   │   ├── Category.cs
        │   │   ├── ExcludedApp.cs
        │   │   ├── ExpansionHistory.cs
        │   │   ├── ExportData.cs
        │   │   └── Snippet.cs
        │   └── Repositories/
        │       ├── CategoryRepository.cs
        │       ├── ExcludedAppRepository.cs
        │       ├── SettingsRepository.cs
        │       └── SnippetRepository.cs
        ├── Helpers/           # ユーティリティ
        ├── Interop/           # Win32 API
        │   └── NativeMethods.cs
        ├── ViewModels/        # MVVM ViewModel
        │   ├── MainViewModel.cs
        │   ├── SearchPopupViewModel.cs
        │   └── SettingsViewModel.cs
        └── Views/             # XAML UI
            ├── MainWindow.xaml(.cs)
            ├── SearchPopupWindow.xaml(.cs)
            └── SettingsWindow.xaml(.cs)
```

## データ保存

- データベース: `%LOCALAPPDATA%\Yobidashi\yobidashi.db` (SQLite)
- バックアップ: `%LOCALAPPDATA%\Yobidashi\backups\`
- エクスポート: JSON形式

## セキュリティ

- パスワード入力欄では展開機能を自動無効化
- 除外アプリの登録が可能
- キー入力の無制限保存は行わない（バッファは最大100文字で自動削除）
- 完全ローカル動作（ネットワーク送信なし）

## 今後の拡張予定

- [ ] 入力型変数 `{{input:名前}}`
- [ ] 候補ポップアップからの展開モード
- [ ] ひらがな⇔カタカナ自動正規化
- [ ] 全角半角自動正規化
- [ ] リッチテキスト対応
- [ ] テーマカスタマイズ
- [ ] MSIX パッケージ配布
- [ ] WinGet 登録

## テスト観点

- [ ] 日本語トリガーでの展開動作
- [ ] IME未確定中の非反応
- [ ] 長文（1000文字以上）の展開
- [ ] 変数展開の正確性
- [ ] 除外アプリでの非動作
- [ ] パスワードフィールドでの非動作
- [ ] グローバルホットキーの動作
- [ ] 検索ポップアップの日本語検索
- [ ] エクスポート/インポートの整合性
- [ ] 常駐時のメモリ使用量

## ライセンス

MIT License

## 作者

Yobidashi Project
