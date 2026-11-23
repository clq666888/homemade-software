# 开发与维护

本工具使用 C#、Windows Forms 和系统自带的 .NET Framework 编译器，无需安装额外软件包。成品仍是根目录的 `磁盘清理工具.exe`。

## 源码位置

| 位置 | 职责 |
| --- | --- |
| `Program.cs` | 程序入口，仅负责启动窗口 |
| `src/Models/` | 文件条目、扫描结果、配置文件的数据格式 |
| `src/Services/FolderSizeScanner.cs` | 递归大小统计、取消扫描、目录访问错误检测 |
| `src/Services/FolderSizeCache.cs` | 大小缓存与当前页、删除影响范围的失效规则 |
| `src/Services/ConfigurationStore.cs` | JSON 配置和自动保存记录的读写、基本校验 |
| `src/Interop/WindowsShell.cs` | Windows 属性窗口及文件类型图标 |
| `src/UI/MainForm.cs` | 窗口初始化、共享状态、排序定时器 |
| `src/UI/MainForm.Layout.cs` | 控件布局、列宽自适应 |
| `src/UI/BufferedDataGridView.cs` | 双缓冲表格绘制，减少滚动时逐行刷新造成的闪烁 |
| `src/UI/MainForm.Browser.cs` | 打开目录、刷新当前页或选中项 |
| `src/UI/MainForm.Scanning.cs` | 后台扫描与界面的衔接、大小显示 |
| `src/UI/MainForm.SearchAndSort.cs` | 名称筛选、高亮、排序、滚动位置 |
| `src/UI/MainForm.Selection.cs` | 多选、快捷键、行颜色 |
| `src/UI/MainForm.FileActions.cs` | 右键菜单、回收站、复制、属性和删除保护 |
| `src/UI/MainForm.Configuration.cs` | 状态、备注编辑及配置保存/导入对话框 |
| `tests/RegressionChecks.cs` | 搜索、多选、排序、缓存及配置兼容检查 |
| `assets/app.ico` | 应用图标的构建源文件 |
| `tools/ExportIcon.cs` | 图标源文件缺失时，从现有 EXE 恢复图标 |

窗口使用 C# `partial` 拆分为多个文件，仍然是同一个窗口类。需要互相协作的界面状态统一放在 `MainForm.cs`；扫描、缓存、配置读写和系统调用已经是独立的类。

## 构建

双击 `build.cmd` 可以重新编译并更新根目录 EXE。程序正在运行时，脚本会先请求正常退出，等待 8 秒后再尝试结束进程；若它以管理员身份运行，构建脚本也需要相应权限，或者先手动关闭软件。

在 PowerShell 中运行：

```powershell
# 编译、执行检查，然后更新 EXE，最后清理构建临时文件
.\build.ps1 -Check

# 只编译并检查，保留结果供调试，暂不替换现有 EXE
.\build.ps1 -Check -BuildOnly
```

临时文件和测试数据均位于本目录的 `.build` 内。普通构建成功后自动删除 `.build`；使用 `-BuildOnly` 或构建失败时会保留，便于排查。根目录的状态、备注、上次目录和用户导出的配置不会被构建脚本覆盖。

`DiskCleanupTool.csproj` 可用于 IDE 或 MSBuild。此电脑只有运行环境，没有 .NET Framework 4 的开发引用程序集；MSBuild 可以使用运行时程序集构建，但会提示警告。日常维护推荐使用已经验证的 `build.ps1`，不需要下载开发包。

## 修改时需要保持的行为

- 后台线程只负责扫描，通过 UI 线程写入缓存和更新控件。缓存类当前由 UI 线程访问。
- 切换目录通过扫描版本号取消旧任务，旧结果不能写回新页面。
- 刷新当前页只使直接子目录的缓存失效；删除只使对应路径和其上级的缓存失效。
- 筛选隐藏项目时，也必须从操作选择集合移除它，避免批量操作隐藏项。
- 排序和重载列表时注意多选与滚动位置；拖选和编辑时推迟自动排序。
- 滚动期间暂停自动排序，停下约 0.25 秒后恢复；行样式没有变化时避免重复设置。
- 文件删除始终使用回收站，失败时不切换为永久删除。
- `status.json`、`notes.json` 及版本 1 的导出配置格式继续兼容；记录以完整路径匹配，缺失路径不造成加载错误。

检查使用 `.build/checks` 内的模拟记录和测试文件，覆盖大小扫描、取消扫描、缓存范围、配置往返、无效配置、筛选、多选、实时排序及滚动恢复。检查不会删除用户文件，也不会实际打开属性对话框。
