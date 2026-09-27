# Resolve Splash Studio

一个 Windows 原生 GUI 工具，用于从 DaVinci Resolve 的 `Resolve.exe` 中定位内嵌的启动图 PNG，并以**文件总长度完全不变**的方式做精确替换

> ⚠️ 这是一个非官方、社区自制的修改工具，与 Blackmagic Design 无关，也未获其认可。请自行承担使用风险，并务必保留原始 `Resolve.exe` 的备份。
> ⚠️ 本项目由人工智能体构建-使用CODEX-DeepSeekV4构建

---

## 功能特性

- **自动定位** `C:\Program Files\Blackmagic Design\DaVinci Resolve\Resolve.exe`
- **扫描 EXE 内所有有效 PNG 资源**，不依赖固定偏移、不锁死版本
- **丰富过滤**：按尺寸、字节偏移、颜色类型、字节数筛选
- **启动图预筛**：标记疑似启动图的资源（宽高比约 2.15–2.42），例如：
  - `2220 x 980`
  - `1110 x 490`
  - `1070 x 450`
- **多选与批量**：支持 Shift/Ctrl 多选、鼠标拖拽框选、全选同尺寸；同尺寸启动图可一次批量替换
- **原图 / 新图双预览**
- **严格尺寸匹配**：新 PNG 必须与原 PNG 宽高完全一致
- **长度保持的补丁引擎**：用合法的 PNG 辅助 chunk + zlib 重压缩，把新图补齐到原图字节数
- **重算所有 PNG chunk 的 CRC**，以及 **PE Header Checksum**
- **可选的 Authenticode 处理**：清除已失效的安全目录
- **每次运行生成 JSON 清单 + SHA-256**
- **支持直接应用、自动备份、一键恢复**

---

## 工作原理

DaVinci Resolve 从内嵌在 `Resolve.exe` 中的 PNG 资源加载启动画面。由于图片由 PE 资源表索引，直接替换会破坏偏移。本工具的做法是：

1. 解析 PE 资源树，找出所有有效 PNG。
2. 对替换图片重新编码，并用合法的 PNG 辅助 chunk + 重压缩的 zlib 数据补齐到**与原图完全相同的字节长度**。
3. 重算所有 PNG chunk 的 CRC。
4. 用 Windows `imagehlp.dll` API 重算并复核 PE Header Checksum。
5. 生成补丁副本（或直接应用并备份），并输出 JSON 清单与 SHA-256。

已在真实 **640 MB** `Resolve.exe`（DaVinci Resolve 20.3.2.9）上验证：输入与输出文件长度完全一致，补丁后的 PNG 可重新解析，校验和全部通过。

---

## 运行要求

- **Windows 10 / 11**
- **.NET Framework 4.8**（Windows 10/11 通常已内置）
- 不需要 Python，不需要 Photoshop 插件，无需额外 SDK

---

## 使用步骤

1. **完全退出** DaVinci Resolve。
2. 运行 `ResolveSplashStudio.exe`（直接应用到 Program Files 时，请**右键 → 以管理员身份运行**）。
3. 确认 `Resolve.exe` 路径后点击 **“扫描”**。
4. 保持勾选 **“仅显示疑似启动图”**。
5. 选择要替换的资源，例如 `2220 x 980`。
   - 按住 `Shift` / `Ctrl` 点击可多选。
   - 在列表中按住鼠标左键拖拽可框选。
   - 也可以选中任意一张后点击 **“全选同尺寸”**。
6. 点击 **“选择新的 PNG...”**。
   - 批量替换时，所有所选原图必须尺寸完全一致。
   - 新图必须正好是同一个宽高。
   - 如果提示无法精确适配，请重新导出 PNG，减少元数据或改用不同的压缩设置。
7. 先点击 **“生成补丁副本”** 做安全测试。
   - 输出文件例如 `Resolve.patched.exe`，同目录会生成 `.manifest.json`。
8. 确认无误后，再点击 **“直接应用到 Resolve.exe”**。
   - 首次应用前会创建 `Resolve.exe.original.bak` 备份。

> 如果启动画面没有变化，可能是 Resolve 根据 DPI 使用了另一档尺寸。可以再选择相同尺寸的 `1110 x 490` 或 `1070 x 450` 资源重复替换；选择 **“同时替换内容完全相同的副本”** 可一次处理字节内容完全一致的重复资源。

---

## 从源码构建

仅需 Windows 内置的 C# 编译器（`csc.exe`），无需 Visual Studio 或 .NET SDK。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

产物输出到 `build/` 目录：

- `ResolveSplashStudio.exe` — GUI 应用程序
- `CoreTests.exe` / `PatchTests.exe` — 核心与真实 640 MB 文件补丁验证
- `BatchUiSmoke.exe` / `BatchPatchTest.exe` — 批量 GUI 与补丁测试

### 源码结构

```
src/ResolveSplashStudio/
├── Core.cs                 # PE 解析、PNG 扫描/重建、CRC、PE Checksum、补丁与清单
├── Gui.cs                  # WinForms GUI
├── Tests.cs / PatchTests.cs          # 核心 + 真实 640 MB 文件补丁测试
├── UiSmoke.cs / BatchUiSmoke.cs      # GUI 扫描、框选、19 张同尺寸批量替换烟测
├── BatchPatchTest.cs                 # 真实 640 MB 文件一次写入 19 个资源
├── InflateTest.cs / SignatureClearTest.cs
└── UiBatchScreenshot.cs
build.ps1                   # 使用 Windows 内置 C# 编译器构建
assets/app.ico              # 应用程序图标
```

---

## 参考 / 灵感来源

- [Custom DaVinci Resolve splash screen (r/davinciresolve)](https://www.reddit.com/r/davinciresolve/comments/1k2jy2d/custom_davinci_resolve_splash_screen/)

---
