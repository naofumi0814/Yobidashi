# Yobidashi（よびだし） - 設計ドキュメント

## 1. アプリ全体の設計方針

### コンセプト
日本語ユーザーが日常的に使う定型文・テンプレートを、トリガー文字列で自然に呼び出せるWindows常駐アプリ。
英語圏のtext expanderの焼き直しではなく、日本語IMEとの共存を前提に設計する。

### 基本方針
- Windows 11 主対象
- 完全ローカル動作（ネットワーク不要）
- トレイ常駐型
- 日本語UI標準
- IMEを改造しない（確定済みテキストのみ対象）

## 2. 技術構成

| 項目 | 選定 | 理由 |
|------|------|------|
| 言語 | C# (.NET 8) | Windows API統合、パフォーマンス |
| UI | WinUI 3 (Windows App SDK) | モダンUI、XAML、Mica/Acrylic |
| DB | SQLite (Microsoft.Data.Sqlite) | 軽量、ローカル完結 |
| キーフック | Win32 API (SetWindowsHookEx) | グローバルキー監視 |
| IME判定 | Win32 API (ImmGetContext) | IME未確定状態の検知 |
| DI | Microsoft.Extensions.DependencyInjection | テスタブル設計 |
| テキスト操作 | SendInput / Clipboard API | テキスト展開 |

### なぜ WinUI 3 か
- XAML による宣言的UI → モダンデザインが容易
- Mica/Acrylic → Windows 11 ネイティブな見た目
- .NET 8 との統合 → パフォーマンスとメンテナンス性
- Win32 API 呼び出し可能 → グローバルフック、IME連携

## 3. MVP機能一覧

### 必須機能
1. システムトレイ常駐
2. Windows起動時の自動起動
3. 定型文CRUD（タイトル、トリガー、本文、カテゴリ、タグ、メモ）
4. SQLite保存
5. 日本語検索（タイトル、トリガー、本文、タグ）
6. グローバルホットキーで検索ポップアップ表示
7. トリガー文字列 + Space/Tab で展開
8. 日本語トリガー対応（ひらがな、カタカナ、漢字まじり）
9. IME未確定中は展開しない
10. 基本変数展開（{{date}}, {{time}}, {{clipboard}}, {{cursor}}）
11. 除外アプリ設定
12. 一時停止モード

## 4. 画面構成案

```
┌─────────────────────────────────────────────────────────────┐
│  🔍 検索バー                              [⏸ 一時停止] [⚙]│
├──────────┬────────────────────┬──────────────────────────────┤
│          │                    │                              │
│ カテゴリ │   定型文一覧       │    詳細編集フォーム          │
│ ──────── │   ──────────       │    ──────────────            │
│ すべて   │ ▶ じゅうしょ       │  タイトル: [          ]      │
│ ビジネス │   しょめい         │  トリガー: [          ]      │
│ あいさつ │   へんしん         │  カテゴリ: [▼ ビジネス]      │
│ SNS     │   おれい           │  タグ:     [          ]      │
│ メール   │   よやく           │  メモ:     [          ]      │
│ その他   │                    │                              │
│          │                    │  本文:                       │
│          │                    │  ┌────────────────────────┐  │
│          │                    │  │                        │  │
│          │                    │  │                        │  │
│          │                    │  └────────────────────────┘  │
│          │                    │                              │
│          │                    │  プレビュー:                 │
│          │                    │  ┌────────────────────────┐  │
│          │                    │  │ (変数展開後のプレビュー)│  │
│          │                    │  └────────────────────────┘  │
│          │                    │                              │
│          │                    │  [保存]  [削除]              │
├──────────┴────────────────────┴──────────────────────────────┤
│  定型文: 42件 │ 最終更新: 2024-01-15  │ 状態: 監視中         │
└─────────────────────────────────────────────────────────────┘
```

### 検索ポップアップ（グローバルホットキーで呼び出し）
```
┌──────────────────────────────────┐
│  🔍 キーワードを入力...          │
├──────────────────────────────────┤
│  じゅうしょ → 〒100-0001 東京...│
│  しょめい   → 山田太郎 / 株式...│
│  へんしん   → いつもお世話に... │
│  おれい     → このたびは誠に... │
├──────────────────────────────────┤
│  ↑↓で選択 / Enterで挿入 / Escで閉じる  │
└──────────────────────────────────┘
```

## 5. データベース設計

### snippets テーブル
| カラム | 型 | 説明 |
|--------|------|------|
| id | INTEGER PRIMARY KEY | 主キー |
| title | TEXT NOT NULL | タイトル |
| trigger_text | TEXT NOT NULL | トリガー文字列 |
| body | TEXT NOT NULL | 本文（最大制限なし） |
| category | TEXT | カテゴリ |
| tags | TEXT | タグ（JSON配列） |
| memo | TEXT | メモ |
| is_enabled | INTEGER DEFAULT 1 | 有効/無効 |
| use_count | INTEGER DEFAULT 0 | 使用回数 |
| created_at | TEXT | 作成日時(ISO8601) |
| updated_at | TEXT | 更新日時(ISO8601) |
| last_used_at | TEXT | 最終使用日時 |

### categories テーブル
| カラム | 型 | 説明 |
|--------|------|------|
| id | INTEGER PRIMARY KEY | 主キー |
| name | TEXT NOT NULL UNIQUE | カテゴリ名 |
| sort_order | INTEGER DEFAULT 0 | 表示順 |

### excluded_apps テーブル
| カラム | 型 | 説明 |
|--------|------|------|
| id | INTEGER PRIMARY KEY | 主キー |
| process_name | TEXT NOT NULL | プロセス名 |
| display_name | TEXT | 表示名 |

### settings テーブル
| カラム | 型 | 説明 |
|--------|------|------|
| key | TEXT PRIMARY KEY | 設定キー |
| value | TEXT | 設定値 |

### expansion_history テーブル
| カラム | 型 | 説明 |
|--------|------|------|
| id | INTEGER PRIMARY KEY | 主キー |
| snippet_id | INTEGER | 定型文ID |
| expanded_at | TEXT | 展開日時 |
| app_name | TEXT | 展開先アプリ名 |

## 6. 主要クラス設計

### レイヤー構成
```
Yobidashi.App          - WinUI 3 アプリケーション
├── Views/             - XAML画面
├── ViewModels/        - MVVM ViewModel
├── Core/              - コアロジック
│   ├── Services/      - ビジネスロジック
│   ├── Hooks/         - キーボードフック
│   └── Expansion/     - テキスト展開エンジン
├── Data/              - データアクセス
│   ├── Models/        - データモデル
│   └── Repositories/  - リポジトリ
├── Interop/           - Win32 API
└── Helpers/           - ユーティリティ
```

### 主要クラス
| クラス | 責務 |
|--------|------|
| App | アプリケーションエントリポイント |
| MainWindow | メイン管理画面 |
| SearchPopupWindow | 検索ポップアップ |
| SettingsWindow | 設定画面 |
| TrayIconManager | システムトレイ管理 |
| GlobalKeyboardHook | グローバルキーボードフック (Win32) |
| ImeStateDetector | IME未確定状態の検知 (Win32) |
| TriggerDetector | トリガー文字列の検知・バッファ管理 |
| TextExpander | テキスト展開（削除→挿入） |
| VariableProcessor | 変数展開（{{date}}等） |
| TextNormalizer | 文字列正規化（全角半角、かなカナ） |
| SnippetRepository | 定型文のCRUD |
| DatabaseManager | SQLite接続管理 |
| SettingsService | アプリ設定管理 |
| HotkeyManager | グローバルホットキー管理 |
| AutoStartManager | Windows起動時の自動起動設定 |
| ExportImportService | JSON エクスポート/インポート |

## 7. イベント処理の考え方

### キーボードフック → トリガー検知 → 展開 のフロー
```
[キー入力]
    ↓
[GlobalKeyboardHook] ← WH_KEYBOARD_LL
    ↓
[除外アプリ判定] → 除外なら無視
    ↓
[一時停止中?] → はいなら無視
    ↓
[IME状態確認] ← ImmGetContext / ImmGetCompositionString
    ↓ (未確定中なら無視)
[TriggerDetector] ← 確定済み文字をバッファに追加
    ↓
[Space/Tab検知] → トリガー候補をバッファから取得
    ↓
[SnippetRepository] → トリガー文字列で検索
    ↓ (一致あり)
[TextExpander]
    ├── トリガー文字列 + Space/Tab を Backspace で削除
    ├── [VariableProcessor] で変数展開
    └── SendInput または Clipboard で本文を挿入
```

## 8. 日本語IMEとの共存方針

### 基本戦略
1. **IMEを改造しない** - あくまで確定後のテキストを監視
2. **未確定状態の検知** - `ImmGetCompositionString` で未確定文字列の有無を確認
3. **確定文字のみバッファリング** - `WM_KEYDOWN` ではなく、確定後の文字入力のみ追跡

### IME状態の判定方法
- `ImmGetContext()` でIMEコンテキスト取得
- `ImmGetCompositionString(hIMC, GCS_COMPSTR)` で未確定文字列取得
- 未確定文字列が空 → 確定済み or 直接入力
- 未確定文字列が存在 → 変換中（展開しない）

### 日本語入力時の動作シーケンス
```
ユーザー操作: 「じゅうしょ」+ Space + Space(展開トリガー)

1. 「j」入力 → IME未確定中 → バッファに追加しない
2. 「u」入力 → IME未確定中 → バッファに追加しない
3. ...
4. 「じゅうしょ」変換候補表示 → IME未確定中
5. Enter/確定 → 「じゅうしょ」確定 → バッファに「じゅうしょ」追加
6. Space押下 → トリガー判定 → 「じゅうしょ」が登録済み → 展開!
```

### 文字列正規化レイヤー
```csharp
public interface ITextNormalizer
{
    string Normalize(string input);
}
```
- 初期MVPでは完全一致
- 将来的に: 全角半角変換、ひらがな⇔カタカナ、大文字小文字

## 9. 実装ステップ

### Step 1: プロジェクト構造作成
- WinUI 3 プロジェクト作成
- NuGet パッケージ追加
- フォルダ構成

### Step 2: データレイヤー
- SQLite スキーマ
- モデルクラス
- リポジトリ

### Step 3: コアサービス
- キーボードフック
- IME状態検知
- トリガー検知
- テキスト展開エンジン
- 変数処理

### Step 4: UI - メインウィンドウ
- 3カラムレイアウト
- 定型文CRUD
- 検索・フィルタ

### Step 5: UI - 検索ポップアップ
- グローバルホットキー
- インクリメンタル検索
- キーボード操作

### Step 6: UI - 設定画面
- 一般設定
- 展開設定
- 除外アプリ
- データ管理

### Step 7: システム統合
- トレイアイコン
- 自動起動
- 一時停止モード

## 10. 今後の拡張ポイント

- 入力型変数 `{{input:名前}}`
- 候補ポップアップ展開モード
- リッチテキスト対応
- クラウド同期（オプション）
- 多言語対応
- テーマカスタマイズ
- 正規表現トリガー
- 条件付き展開
- マクロ機能

## 11. テスト観点

- トリガー検知の正確性（日本語・英数字・混在）
- IME未確定中の非反応
- 変数展開の正確性
- SQLiteのCRUD操作
- 検索の日本語対応
- 除外アプリの判定
- 長文展開のパフォーマンス
- グローバルホットキーの衝突
- メモリリーク（常駐アプリとして）
- 複数モニター対応

## 12. 配布構成案

- MSIX パッケージ（Microsoft Store 配布可能）
- サイドロード用 MSIX
- ポータブル版（単一実行ファイル）
- WinGet 登録
