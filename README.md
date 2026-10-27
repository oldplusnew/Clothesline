# Clothesline

<img src="https://oldplusnew.github.io/Clothesline/hero.png" alt="Clothesline：几张截图用晾衣夹挂在屏幕顶部的一条晾衣绳上" width="100%">

**Windows 版「晾衣绳」：截图随手一挂，桌面干干净净。**

指针蹭一下屏幕最上沿，绳就滑下来，上面挂着刚拍的截图；指针离开，它自己收回去。

作者：[oldplusnew](https://github.com/oldplusnew)

> **在线介绍页（含下载按钮、微信可打开）：** https://oldplusnew.github.io/Clothesline/

## 第一次用？四步上手

> 它**没有主窗口**。双击 exe 之后屏幕上看不到任何东西，这是正常的——它躲在屏幕右下角的**任务栏托盘**里，图标是一枚**晾衣夹**。
> 首次启动会自动弹一张说明卡片；日后随时可在托盘图标上点右键 →「使用说明」重看。

1. **把鼠标推到屏幕最上沿**：绳子滑下来，不用了它自己收回去。（想让它**一直挂着**？按 `Ctrl + Alt + T`、点绳子右端那枚小图钉，或托盘右键勾「固定在桌面」——固定后下次开机它还在那儿）
2. **截个图就挂上了**：微信 `Alt + A`、QQ `Ctrl + Alt + A`、`Win + Shift + S`、`Win + PrtScn` 都行。
   微信 / QQ 的截图**只进剪贴板、不存盘**，Clothesline 直接从剪贴板接住它，约 1 秒内挂到绳上。
3. **单击复制、双击打开**：按住拖出去可发给微信 / Word；鼠标停在卡片上会出现 `✕`，点了丢进回收站。
4. **想挂一张旧图片**：见下面「三条路」，随便挑一条。

### 想把已有的图片挂上去？三条路，随便挑一条

| 方式 | 操作 |
| --- | --- |
| **拖上去**（最直观） | 从资源管理器 / 桌面按住一个或多个文件，拖到屏幕顶部——绳子会自动滑下来接住，松手即挂。网页里的图片也能直接拖过来。 |
| **右键菜单** | 在图片（或视频）上右键 →「**挂到晾衣绳**」。Win11 的右键菜单默认精简，该项在「**显示更多选项**」里，或按 `Shift + F10`。 |
| **托盘点选** | 左键或右键单击托盘里的晾衣夹图标 →「**挂张图片…（选择文件）**」，在文件对话框里多选即可。 |

截图是**自动挂载**的，不用任何操作，两条路都覆盖：

| 截图方式 | 走哪条路 | 说明 |
| --- | --- | --- |
| 微信 `Alt + A`、QQ `Ctrl + Alt + A`、`Win + Shift + S` | **剪贴板监听** | 这类截图只进剪贴板、不落盘，图一进剪贴板就挂上（默认开） |
| `Win + PrtScn`、系统录屏 | **文件夹监听** | 监听 `图片\Screenshots` 与 `视频\Captures`，新文件一落地就挂上 |

两路撞车时（`Win + Shift + S` 既写剪贴板又写文件）会按「5 秒内同尺寸」去重，只挂一张，不会变双份。

托盘图标右键菜单：固定在桌面（不自动收起）、挂张图片…、图片右键菜单开关、截图/复制的图片自动挂上、开机自动启动、打开截图文件夹、使用说明、退出。

---

## 出处与致敬 · Credits

> **本项目改编自 [Tendedero](https://github.com/alejandrobujan/tendedero)** —— Alejandro Buján 为 macOS 写的一个小软件（Swift / AppKit / SwiftUI，MIT 许可）。
>
> Tendedero 只有 macOS 版。这里是一次**行为对齐的独立重写**：C# + WPF，**不含原作任何源代码**，但交互设计、交互手势、功能取舍乃至"晾衣绳"这个隐喻，全部来自原作——**这里每一个好点子都归功于原作者**。
>
> 原作以 MIT 授权其代码，但**名称与图标不在授权范围内**，因此本项目另取名为 Clothesline（英语"晾衣绳"），图标另行绘制，与原作者无从属关系，也不代表其背书。
>
> 若你在用 macOS，请直接去用原版，它更地道：[alejandrobujan/tendedero](https://github.com/alejandrobujan/tendedero)

原作的完整版权声明保留在 [LICENSE](LICENSE) 中。

## 它做什么

- 截图不再落在桌面。`Win + PrtScn` 拍下的图，都挂到屏幕顶部那条绳上。
- **微信 `Alt + A` / QQ 截的图也挂**：这类截图只进剪贴板，Clothesline 直接接住，不用另存。
- 绳平时藏着。指针触到屏幕最上沿，它滑下来；指针离开，收回去。
- 单击复制、双击打开、拖进微信/Word 直接用、拖进文件夹就归位、点 ✕ 丢进回收站。
- 全程本地运行：不联网、不上传、无账号、无统计。

## macOS 原版 → Windows 版映射

| Tendedero（macOS） | Clothesline（Windows） |
| --- | --- |
| 指针停在菜单栏，绳滑下 | 指针触屏幕最上沿，绳滑下（1.8 秒后自动收回） |
| `⌃⌥T` 显示 / 隐藏 | `Ctrl + Alt + T` |
| 单击 → 复制图片 | 单击 → 复制图片（浮现"已复制"） |
| 双击 → 预览打开 | 双击 → 用默认看图程序打开 |
| 长按 → 标记（Markup） | 暂未实现，双击后用系统"画图/照片"编辑 |
| 拖进应用 → 发送副本，仍挂在绳上 | 同左 |
| 拖进文件夹 → 文件归位，离开晾绳 | 同左（自动识别资源管理器） |
| 拖到废纸篓 / 点叉 → 丢弃 | 点 ✕ → 进回收站 |
| 复制来的图片也挂上（可选） | 托盘菜单「截图 / 复制的图片自动挂上」，**默认开**，覆盖微信 `Alt + A`、QQ 截图与网页复制的图片 |
| 全屏时自动隐身 | 同左 |
| 屏幕录制也挂在绳上，可裁剪 | 录屏以 ▶ 占位显示，可打开；裁剪暂未实现 |

## 安装

到 [Releases](https://github.com/oldplusnew/Clothesline/releases/latest) 下载 `Clothesline.exe`，双击即可运行。

- 单文件、自包含（约 63 MB），**不需要安装 .NET**
- 无安装程序、不写系统目录、不注册服务；设置存于 `%APPDATA%\Clothesline\settings.json`

首次运行后，托盘会出现一枚"晾衣夹"图标：
- **左键**：固定在桌面 / 取消固定（收起）
- **右键**：菜单（固定在桌面（不自动收起）、挂张图片…、截图/复制的图片自动挂上、开机自动启动、打开截图文件夹、使用说明、退出）

固定在桌面后，晾绳会一直挂着：**鼠标不在绳上时它自动"穿透"，不挡住屏幕顶部的窗口操作**；鼠标移回绳上即可正常点图、点图钉。这个状态会被记住，重启后照旧。

> 剪贴板挂来的图会在 `%APPDATA%\Clothesline\Hang\` 存一份 PNG 副本（拖拽与复制都要有真实文件可用）。那是程序自己的目录，删掉 exe 时可一并清空。

## 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)。

```bash
git clone https://github.com/oldplusnew/Clothesline.git
cd Clothesline
dotnet build -c Release
dotnet publish Clothesline.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish
```

> 注意：**必须自包含 + `IncludeNativeLibrariesForSelfExtract=true`**。框架依赖的单文件发布会因 WPF 原生库（`wpfgfx_cor3.dll`）探测失败而启动即崩。

图标由 `gen-icon.ps1`（GDI+ 绘制，输出多尺寸 ICO）生成；托盘图标在运行时代码绘制。

## 已知局限

- 只覆盖**主显示器**（多屏支持待办）
- 屏幕上沿若被"任务栏置顶"占用，会与触顶唤出冲突
- 微信 / QQ 的截图能挂上，前提是它们的截图默认"复制到剪贴板"（微信默认如此，未改过就没问题）
- 录屏裁剪、长按标记、自定义快捷键：留待 0.2

## 代码结构

| 文件 | 职责 |
| --- | --- |
| `Program.cs` | 入口、单实例、`--hang` / `--guide` 参数、命名管道转交、首启判断 |
| `LineWindow.cs` | 顶部透明置顶面板：唤出/收起、挂图、剪贴板、拖放、热键 |
| `HangItem.cs` | 一张图：玻璃相框、晾衣夹、摆动/微风、拖拽与点击 |
| `OnboardingWindow.cs` | 首次启动的那张说明卡片（四步上手 + 三个开关） |
| `ShellMenu.cs` | 注册 / 注销资源管理器右键「挂到晾衣绳」 |
| `ScreenshotWatcher.cs` | 监听 `图片\Screenshots`、`视频\Captures` |
| `MouseHook.cs` | 全局低级鼠标钩子（探测触顶） |
| `TrayIcon.cs` | 托盘图标（代码绘制 HICON）与右键菜单 |
| `Native.cs` | 全部 Win32 P/Invoke |
| `Settings.cs` | 设置持久化（`%APPDATA%\Clothesline\settings.json`） |
| `Diag.cs` | 日志落盘（`%APPDATA%\Clothesline\log.txt`），排障用 |

## 支持作者

Clothesline **永久免费**：源码开放、无广告、无账号、不联网。个人用、公司用，都不必付费。

如果它确实替你省了事，可以扫下面这张码请我喝杯茶：

<img src="https://oldplusnew.github.io/Clothesline/alipay.png" alt="支付宝收款码（Oldplusnew）" width="240">

> 打赏完全自愿。它不会解锁任何功能——所有功能本来就都给你；也不换来"优先处理"，issue 一视同仁地看。

## 许可

[MIT](LICENSE) —— 含 Tendedero 原作者 Alejandro Buján 的原始版权声明。名称与图标不在 MIT 范围内，见 LICENSE 末尾说明。

---

### In English

Clothesline is a Windows reimplementation of [Tendedero](https://github.com/alejandrobujan/tendedero) by Alejandro Buján — a tiny macOS app that hangs your screenshots on a line at the top of the screen. Original design, gestures and behaviour spec all belong to its author; this project is an independent C#/WPF rewrite with no code taken from it, under the same MIT license, with a different name and a separately drawn icon.
