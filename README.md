# 里程碑

<img src="Assets/logo-y.png" alt="里程碑 Y 图标" width="128">

一个简洁的离线项目进度记录工具：**一行一个项目，从左到右记录里程碑，最右侧就是当前进度。**

无需账号，使用时无需联网，数据保存在自己的电脑上。

## 直接下载

普通用户下载下面对应系统的 **ZIP 压缩包**即可，不需要安装编程工具，也不需要编译源码。

| 你的电脑 | 下载文件 | 当前版本 | 系统要求 |
| --- | --- | --- | --- |
| **Mac（Apple 芯片）** | **[下载 Mac 版](https://github.com/3545483499/milestone/releases/download/macos-v1.0.3/milestone-macos-1.0.3-arm64.zip)** | macOS 1.0.3 | macOS 14 或更新版本，Apple 芯片；目前没有 Intel Mac 安装包 |
| **Windows** | **[下载 Windows 版](https://github.com/3545483499/milestone/releases/download/v1.1.1/milestone-windows-1.1.1.zip)** | Windows 1.1.1 | Windows 10 1607+ / Windows 11，系统 .NET Framework 4.6.2+ |

[查看全部发行版本与校验文件](https://github.com/3545483499/milestone/releases)。Mac 和 Windows 独立编号，版本号不同不代表下载错了。

> 在 Releases 页面展开 **Assets（资源）**，按上述文件名下载。`Source code (zip)` / `Source code (tar.gz)` 是源码，不是可以直接运行的软件。Windows 请使用 1.1.1 或更新版本，1.1.0 存在高 DPI 布局问题。

## Mac：下载后怎么打开

1. 点击上面的 **下载 Mac 版**。
2. 双击下载的 `milestone-macos-1.0.3-arm64.zip` 解压。
3. 将解压后的 **里程碑.app** 拖进 Finder（访达）的 **应用程序** 文件夹。
4. 在「应用程序」中双击 **里程碑** 打开。

不知道自己的 Mac 类型：点左上角 ** → 关于本机**。显示“芯片 Apple M…”可使用此安装包；显示 Intel 的 Mac 暂不支持。

### 首次打开提示“无法验证开发者”

当前 Mac 包采用临时签名，尚未经过 Apple 公证。确认下载自本仓库且愿意运行后，可按照 [Apple 官方说明](https://support.apple.com/zh-cn/102445)，先尝试打开一次，再到 **系统设置 → 隐私与安全性** 找到对应的拦截提示，选择 **仍要打开**，并确认“打开”。受管理的电脑可能不允许此操作。

如果提示文件损坏、系统版本不兼容或其他错误，请先重新下载、核对系统要求，并在 [Issues](https://github.com/3545483499/milestone/issues) 提供错误截图和系统版本。

## Windows：下载后怎么打开

1. 点击上面的 **下载 Windows 版**。
2. 右键 `milestone-windows-1.1.1.zip` → **全部解压缩**。
3. 将解压出的文件夹放到方便的位置，例如「文档」或桌面。
4. 双击文件夹里的 **里程碑.exe**。这是免安装版，无需安装向导。

请先解压，再运行 exe。无需安装 Python、Node.js 或 Visual Studio。也可为 exe 创建桌面快捷方式。

Windows 包尚未进行代码签名，首次运行可能出现发布者或信誉提示；确认文件来自本仓库后再决定是否运行，不要关闭系统防护。如果提示缺少 .NET Framework，请通过 Windows 更新或 [微软官方 .NET Framework 下载页](https://dotnet.microsoft.com/download/dotnet-framework) 安装适合系统的版本，再打开应用。

Windows 构建为 AnyCPU。已在 Windows 11 ARM64 虚拟机中以 64 位进程验证；x64 / x86 实机尚未验证。更多技术细节见 [Windows 说明](Windows/README.md)。

## 第一次使用：三步开始

1. **新建项目**：点击右上角「新建项目」，填写项目名称。一行对应一个项目。
2. **添加里程碑**：点击该行右侧「＋ 里程碑」，自由填写进展，例如“完成原理图”“调试通过”。节点文字就是里程碑名称，也可以写多行备注。
3. **保存内容**：回车换行；按下表的保存快捷键，或点击文本框外保存。

| 操作 | Mac | Windows |
| --- | --- | --- |
| 新建项目 | `⌘N` | `Ctrl+N` |
| 文本换行 | `Enter` | `Enter` |
| 保存当前编辑 | `⌘Enter` | `Ctrl+Enter` |
| 鼠标保存 | 点击文本框外 | 点击文本框外 |

- 点击或双击节点文字即可编辑，最近修改时间自动记录到分钟。
- 未修改文字、仅调整节点顺序，不会更新时间。
- 每行**最右侧的里程碑自动代表当前进度**，无需手动设置状态。
- 点击节点右上角 `…` / `···`，可以向左移动、向右移动或删除。
- 点击项目名称重命名；项目旁的菜单可以删除整个项目，删除前会确认。
- 项目或节点较多时，可横向、纵向滚动画布；长文本在文本框内滚动。
- 新添加但尚未填写的节点显示「等待填写」，不会虚构修改时间。

## 更新软件，数据会丢吗

不会因为替换程序文件而清空数据。软件和数据库分开存放。

- **Mac**：先按 `⌘Q` 退出里程碑，再将新版 `里程碑.app` 拖入「应用程序」，替换旧版后重新打开。
- **Windows**：关闭旧版窗口，解压新版，用新的 `里程碑.exe` 替换旧文件，再打开。
- 重要项目建议更新前按下面方法备份。不要删除数据库目录。

## 数据位置、备份与恢复

| 系统 | 数据目录 | 如何打开 |
| --- | --- | --- |
| Mac | `~/Library/Application Support/Milestone/` | Finder 中按 `⌘⇧G`，粘贴左侧路径 |
| Windows | `%LOCALAPPDATA%\Milestone\` | 按 `Win+R`，粘贴左侧路径并回车 |

数据库文件为 `milestones.sqlite`，运行时可能同时存在 `-wal`、`-shm` 文件。

**备份**：退出应用后，复制整个 `Milestone` 数据目录到安全位置。**恢复**：退出应用，先保留当前目录的备份，再用备份目录替换当前数据目录，重新打开应用。

没有账号、云同步或自动跨电脑同步功能。源码仓库和下载包均不包含你的项目数据。

## 开发者：从源码构建

只想使用软件的用户，无需执行本节。

### macOS

需要 macOS 和 Xcode / Swift 6 工具链。默认构建 Apple 芯片版本：

```sh
bash build.sh
bash test.sh
```

输出：`dist/里程碑.app`。使用 SwiftUI、AppKit、系统 SQLite，以及系统 `sips`、`iconutil`、`codesign` 工具。

### Windows

在 Windows PowerShell 中，从项目根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Windows\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Windows\test.ps1
```

输出：`dist\windows\里程碑.exe`。使用 WinForms、系统 .NET Framework C# 编译器和系统 SQLite，不下载 NuGet 包。

Windows 测试覆盖数据库重开、多行与空行、Unicode、计时、排序、删除、编辑控件保存，以及 100% / 125% / 150% / 200% 的布局回归。物理键鼠、输入法与更多设备仍需人工验收。

## 项目结构

```text
Sources/                   macOS 界面、SQLite 存储与节点操作
Assets/                    Y 图标和生成提示词
Tests/                     macOS 数据持久化测试
build.sh / test.sh         macOS 构建与测试
Windows/                   Windows 源码、构建脚本和测试
```

构建产物、缓存、应用备份和本地数据库不纳入 Git。
