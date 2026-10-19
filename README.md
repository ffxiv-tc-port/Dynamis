# Dynamis

[Exter-N/Dynamis](https://github.com/Exter-N/Dynamis) 的台服移植版。
給開發者／逆向工程用的 Dalamud 開發工具箱插件。

## 功能

- 各項 Dalamud API 的檢視介面，例如特徵碼掃描器、物件表
- 物件檢視器：只憑起始位址就能猜測物件的型別與大小，支援
  FFXIVClientStructs 已知型別、其他遊戲物件、函式（內建反組譯器）；部分型別有專屬檢視方式
  （例如材質物件可直接預覽）
- IPFD（In-Process Faux Debugger）：不需外接除錯器即可設中斷點／監看點——不會真的暫停程序，
  而是在命中時擷取該執行緒的快照（CPU 狀態＋完整堆疊），避免因暫停遊戲程序造成與伺服器斷線
- 可選內嵌 PowerShell，用來操作 FFXIVClientStructs、Lumina、Dalamud、Dynamis 本身與其他插件
  （提供含 / 不含內嵌 PowerShell 兩種發行版本，見下方「兩個發行版本」）

Dynamis 的工具與既有的逆向工程工具（IDA/Ghidra/Binja、ReClass、x64dbg/CE 等）有部分重疊，
但設計上是搭配那些工具使用，而非取代。

## 指令

- `/dynamis`：開啟工具箱；`/dynamis help` 在聊天視窗列出所有子指令

## 兩個發行版本

插件庫裡有兩個條目，來自同一份程式碼、用不同建置組態產生，擇一安裝即可：

- **Dynamis**：一般版，不含 PowerShell。
- **Dynamis (with Hosted PowerShell)**（`DynamisWithSMA`）：多帶 PowerShell 執行環境（System.Management.Automation），
  可在遊戲內執行 PowerShell 與下方的自訂 cmdlet；下載檔較大。

## Inter-Plugin API

Dynamis 透過 Dalamud IPC 機制提供多項函式，文件在 `docs/ipc-api.md`。

## 內嵌 PowerShell Cmdlet

提供操作 FFXIVClientStructs、Lumina、Dalamud 等的自訂 cmdlet，文件在 `docs/cmdlets.md`。

## 安裝

在 Dalamud 設定的「自訂插件庫」加入
`https://raw.githubusercontent.com/ffxiv-tc-port/DalamudPluginsTC/main/repo.json`
並啟用，選擇其中一個發行版本安裝。

## 台服版的修改

- 介面繁體中文化
- 啟動時若找不到記憶體釋放函式的特徵碼，插件照常啟動，只有「從解構子估算類別大小」不可用
- 開啟 IPFD 後模組載入失敗時不再連帶結束遊戲
- 中斷點視窗、內嵌 PowerShell 視窗改成鎖外繪製，不再與背景執行緒搶鎖；中斷點的去重模式改成保留第一次命中
- 材質存成 .tex 時 BC4／BC6H 格式不再被寫成「未知格式」
- 資訊列的 IPFD 項目左鍵、右鍵都是開關設定視窗

原作者：[Exter-N](https://github.com/Exter-N)
