# 里程碑 Windows 版

轻量、离线、免安装。解压后双击 **里程碑.exe** 即可使用。

当前版本 **1.1.1**，已修复高 DPI 字体与控件重叠问题。升级时退出旧版，替换 exe 后重新打开；数据库目录保持不变。

## 系统要求

- Windows 10 1607 或更新版本、Windows 11。
- 系统 .NET Framework 4.6.2 或更新版本，以及 Windows 自带的 SQLite。
- AnyCPU 程序，适用于具备相应 .NET Framework 环境的 Windows。已在 Windows 11 ARM64 虚拟机中以 64 位进程通过测试；x64 / x86 实机尚未验证。
- 不需要账号、联网或管理员权限。程序未购买代码签名证书。

## 使用

1. 点击「新建项目」，或按 **Ctrl+N**。
2. 点击项目右侧「＋ 里程碑」，填写进展。
3. **回车换行**；**Ctrl+Enter** 或点击文本框外保存。
4. 最近修改时间自动记录到分钟；只改变换行编码或排序不会更新时间。
5. 通过节点右上角 `···` 向左／向右移动或删除节点，最右侧为当前进度。
6. 点击项目名称重命名；项目旁的 `···` 可删除项目。
7. 内容较多时，在文本框内滚动；项目和节点较多时，使用画布滚动条。

## 数据位置与备份

按 Win+R，输入 `%LOCALAPPDATA%\Milestone` 可打开数据目录。

数据库为 `milestones.sqlite`，可能同时存在 `-wal` 和 `-shm` 辅助文件。备份时先关闭应用，再复制整个目录。更新 exe 不会清空项目。

数据库结构与 macOS 版一致，时间使用相同的参考纪元，文字保存为 UTF-8。需要跨平台迁移时，先退出两端应用，备份各自原目录，再复制完整数据库目录；没有自动同步功能。

## 从源码构建

在 Windows PowerShell 中，从项目根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Windows\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Windows\test.ps1
```

输出位于 `dist\windows\里程碑.exe`。使用系统 .NET Framework C# 编译器，不下载 NuGet 包。

`test.ps1` 使用临时数据库，不接触真实项目数据。覆盖数据库重开、多行和 Unicode、计时规则、排序边界、删除，以及真实 WinForms 编辑控件的命令路由和保存逻辑；同时生成窗口渲染图。物理键鼠、中文输入法与其他 Windows 设备仍需人工验收。

布局回归通过同一生产布局代码分别渲染 100%、125%、150%、200% 四档缩放，检查标题和底栏文字尺寸、卡片内相交、运行时新增/重建、窄窗口及长文本。测试不修改用户的系统显示设置。
