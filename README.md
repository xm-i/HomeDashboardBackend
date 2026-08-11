# HomeDashboard Backend

家庭内の各種情報（家計簿・ネットワーク・アクアリウム・電力・キッチン等）を一元管理するホームダッシュボードの Web API バックエンド。

## 技術スタック

- .NET (ASP.NET Core)
- Entity Framework Core
- PostgreSQL (Npgsql)

## 環境構築

### 1. API サーバーの初回起動

アプリケーションを起動すると、`EnsureCreated()` により必要なテーブルが自動的に作成されます。

### 2. マテリアライズドビューの作成

初回起動後、PostgreSQL に対して以下の SQL スクリプトを実行してください。

```bash
psql -U <user> -d <database> -f Database/Scripts/create_materialized_views.sql
```

このスクリプトにより、`DailyAssetProgresses` マテリアライズドビューが作成されます。  
データの更新は HomeDashboardBatch の `investment update-daily-asset-progress` コマンドで行われます。
