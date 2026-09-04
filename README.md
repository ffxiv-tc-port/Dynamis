# Dynamis

給開發者／逆向工程用的 Dalamud 開發工具箱插件。

## 功能

- 各項 Dalamud API 的檢視介面，例如特徵碼掃描器、物件表
- 物件檢視器：只憑起始位址就能猜測物件的型別與大小，支援
  FFXIVClientStructs 已知型別、其他遊戲物件、函式（內建反組譯器）；部分型別有專屬檢視方式
  （例如材質物件可直接預覽）
- IPFD（In-Process Faux Debugger）：不需外接除錯器即可設中斷點／監看點——不會真的暫停程序，
  而是在命中時擷取該執行緒的快照（CPU 狀態＋完整堆疊），避免因暫停遊戲程序造成與伺服器斷線
- 可選內嵌 PowerShell，用來操作 FFXIVClientStructs、Lumina、Dalamud、Dynamis 本身與其他插件
  （提供含 / 不含內嵌 PowerShell 兩種發行版本）

Dynamis 的工具與既有的逆向工程工具（IDA/Ghidra/Binja、ReClass、x64dbg/CE 等）有部分重疊，
但設計上是搭配那些工具使用，而非取代。

## Inter-Plugin API

Dynamis 透過 Dalamud IPC 機制提供多項函式，文件在 `docs/ipc-api.md`。

## 內嵌 PowerShell Cmdlet

提供操作 FFXIVClientStructs、Lumina、Dalamud 等的自訂 cmdlet，文件在 `docs/cmdlets.md`。

## 安裝

在 Dalamud 設定的「自訂插件庫」加入
`https://raw.githubusercontent.com/ffxiv-tc-port/DalamudPluginsTC/main/repo.json`
並啟用，選擇其中一個發行版本安裝。
