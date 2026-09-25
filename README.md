# 里程碑

<img src="Assets/logo-y.png" alt="里程碑 Y 图标" width="128">

macOS / Windows 离线应用。一个画布，每行一个项目，里程碑从左到右排列，最右侧即当前进度。

macOS 版使用 SwiftUI、AppKit 和系统 SQLite，版本为 **1.0.3**。Windows 版使用 WinForms、.NET Framework 和系统 SQLite，版本为 **1.1.0**。

Windows 用户请查看 [Windows 使用与构建说明](Windows/README.md)。免安装 exe 可从 [GitHub Releases](https://github.com/3545483499/milestone/releases) 下载。

## 使用

以下快捷键及路径针对 macOS；Windows 使用 Ctrl+Enter 保存、Ctrl+N 新建项目。

从源码构建后，双击 `dist/里程碑.app`，也可以自行拖到「应用程序」文件夹。适用于 Apple 芯片 Mac，macOS 14 或更新版本。

- 点击「新建项目」，填写项目名称；快捷键 `⌘N`。
- 点击项目右侧的「＋ 里程碑」，输入自由文本。
- 里程碑文字支持多行：回车换行，`⌘Enter`、点击文本框外或切换到其他节点保存文字。
- 文本区域随内容增高；超过最大高度后在文本框内滚动查看。
- 文字修改后自动显示最近修改时间，格式为 `年-月-日 时:分`；未改文字、仅调整顺序，不更新时间。
- 点击或双击已有节点文字可重新编辑；较长内容在文本框内滚动查看。
- 点击节点右上角的 `…`，可以向左／向右移动或删除。排序后最右侧的节点自动成为当前进度。
- 点击项目名称可重命名；项目旁的 `…` 可以删除项目。删除前会确认。
- 使用触控板或滚动条横向、纵向浏览画布。

新添加但尚未填写的节点显示「等待填写」，不生成虚构的修改时间。

## 数据

数据仅保存在本机 SQLite 数据库，无账号、云同步、网络请求或第三方依赖。

位置：`~/Library/Application Support/Milestone/`

备份时退出应用后复制整个目录；恢复时退出应用，再用备份替换该目录。应用文件与数据分离，重新构建应用不会清空数据。

## 构建与验证

需要 macOS、Xcode / Swift 6 工具链。脚本默认构建 Apple 芯片版本，使用系统自带的 `sips`、`iconutil` 与 `codesign`：

```sh
bash build.sh
bash test.sh
```

构建结果位于 `dist/里程碑.app`，使用本机临时签名，未进行 Apple 公证。

数据库自动测试覆盖多项目保存、重新打开、文字更新时间、不变文字保持时间、节点排序与边界、Unicode、多行与空行文本和删除持久化。界面点击与快捷键需人工验收。

## 项目结构

```text
Sources/App.swift           应用界面和编辑交互
Sources/Database.swift      SQLite 存储与节点操作
Sources/SQLite.h            系统 SQLite 桥接头文件
Assets/                    Y 图标和生成提示词
Tests/StorageTests.swift    数据持久化测试
build.sh                   构建并签名 .app
test.sh                    运行数据库测试
Windows/                   Windows 版源码、构建脚本及说明
```

构建产物、缓存、应用备份和本地数据库均不纳入 Git。
