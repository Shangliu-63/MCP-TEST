# 智慧公文派發分析｜.NET 8 MCP 自學專案

以學校公文派發為情境，練習將 **.NET 8 Blazor、MCP Client、ASP.NET Core MCP Server、離線語意分析與 SQLite** 串成可執行的端到端原型。

> 目前定位為教學／面試展示用的決策支援原型。系統只提供候選承辦人、分數與理由，不會自動進行正式公文派發。所有姓名、信箱與公文內容均為虛構資料。

## 架構

```text
使用者
  ↓
SchoolRouting.Blazor
  ↓ SchoolMcpClient（HTTP /mcp）
SchoolRouting.McpServer
  ├─ SchoolRoutingTools（6 個 MCP tools）
  ├─ DomainSemanticRoutingService（離線混合語意排序）
  └─ SqliteSchoolRepository
       ↓
    data/school.db
```

資料流為：輸入公文主旨與內容 → 呼叫 `semantic_search_responsibilities` → 讀取工作職掌候選 → 計算語意、關鍵字與優先序分數 → 回傳處室、承辦人、符合詞、分數與信心 → 由使用者覆核。

## Solution 結構

| 專案／目錄 | 責任 |
|---|---|
| `src/SchoolRouting.Contracts` | 跨專案共用資料契約 |
| `src/SchoolRouting.Blazor` | 公文輸入、MCP Client、結果表與錯誤呈現 |
| `src/SchoolRouting.McpServer` | MCP HTTP 端點、工具、語意服務與資料存取 |
| `tests-dotnet/SchoolRouting.McpServer.Tests` | Repository 與語意排序測試 |
| `tools-dotnet/SchoolRouting.McpSmokeTest` | MCP Client 端到端連線與工具呼叫驗證 |
| `data` / `scripts` | SQLite schema、測試資料與重建腳本 |

## 內容

- 10 個處室
- 30 位模擬人員
- 60 筆個人工作職掌
- 20 份路由測試公文
- 5 筆歷史派發紀錄

主要資料表為 `departments`、`people`、`responsibilities`、`test_documents` 與 `dispatch_history`。`responsibility_search_view` 提供 MCP 工具後續搜尋職掌時使用。

## 重新建立資料庫

專案只使用 Python 標準函式庫，不需安裝額外套件。

```powershell
python scripts/seed_database.py
```

此命令會重新建立 `data/school.db`，原資料庫內容將被取代。

## 執行驗證

```powershell
python -m unittest discover -s tests -v
```

## .NET 8 專案

以 Visual Studio 開啟 `SchoolRouting.sln`，將 `SchoolRouting.McpServer` 與
`SchoolRouting.Blazor` 設為多重啟動專案。預設網址：

- MCP Server：`http://localhost:5200/mcp`
- Blazor：`http://localhost:5100`

命令列啟動方式：

```powershell
dotnet run --project src/SchoolRouting.McpServer --launch-profile http
dotnet run --project src/SchoolRouting.Blazor --launch-profile http
```

瀏覽 `http://localhost:5100/document-routing`，先按「檢查 MCP 工具」，再測試公文分析。
MCP Server 目前提供 6 個工具，其中 `semantic_search_responsibilities` 會以完整公文進行
領域同義概念、直接關鍵字及中文字元相似度的混合評分。這個離線基準不需要 API Key，
未來可以替換 `ISemanticRoutingService` 的實作，接入雲端或本機 Embedding Model。

MCP Server 啟動後，可以另外執行端對端測試：

```powershell
dotnet run --project tools-dotnet/SchoolRouting.McpSmokeTest
```

## MCP Tools

- `list_departments`
- `get_department`
- `list_people_by_department`
- `get_person_responsibilities`
- `search_responsibilities`
- `semantic_search_responsibilities`

## 離線語意分析

`DomainSemanticRoutingService` 是不需 API Key 的第一版語意基線：

- 語意分數：領域同義概念召回 90% ＋中文字元 bigram Dice 10%
- 最終分數：語意 75% ＋直接關鍵字 20% ＋職掌優先序 5%
- 信心：70 分以上為高、45–69 為中、45 以下建議人工判斷

這不是神經網路 Embedding。`ISemanticRoutingService` 保留替換點，後續可接雲端或本機 Embedding Model、Vector Store 與 AI Agent。

## 測試

```powershell
dotnet test SchoolRouting.sln
```

目前結果：**5/5 xUnit 測試通過**；MCP Smoke Test 可發現並呼叫 6 個工具。

## 後續方向

- 以 Embedding ＋ Vector Store 處理資料庫未出現過的措辭
- 加入 AI Agent，負責工具選擇、追問與派發理由整理
- 將人工覆核結果回寫成歷史權重與評估資料集
- 加入 SSO、RBAC、稽核紀錄、敏感資料遮罩與正式資料庫
- 建立部署、日誌、監控與離線／線上評估流程

## 簡單查詢範例

```sql
SELECT department_name, person_name, description, keywords
FROM responsibility_search_view
WHERE description LIKE '%資訊安全%'
   OR keywords LIKE '%資訊安全%';
```
