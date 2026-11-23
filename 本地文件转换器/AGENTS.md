# 项目开发入口

## 范围与交付
- 唯一项目：`E:\小软件开发\本地文件转换器`，遵循主目录已采用的公共规则；不扫描兄弟项目。
- 当前 0.4.3，Windows x64 原生 WinForms / C# 5 / Framework 4.8，PDF 后端为打包的 Python 3.12。用户不需安装 Python。
- 根入口 `文件万能处理器.exe`，必须保留 `dist/文件万能处理器/engine`。dist 仅当前版。用户明确授权更新时强制关闭本项目旧 EXE，无需再询问；不按名称结束用户 Word。

## 阅读顺序
1. 本文件。
2. `docs/HANDOFF.md` 当前状态、验证边界。
3. `docs/ARCHITECTURE.md` 与本次相关源码；不全量读取依赖/构建输出。

## 主要入口
- `src/MainForm.cs` PDF 工作区/状态；`src/UiControls.cs` 卡片和页面画布。
- `src/PdfEngine.cs` 私有 JSON 进程调用；`engine/pdf_engine.py` PDF/DOC 快速读取与格式导出。
- `src/PageDocument.cs` 页面顺序/撤销/安全覆盖；`src/WordConverter.cs` Word COM 隔离工作进程。
- `src/ResultSession.cs` 结果/原子保存；`src/DownloadJob.cs` 批量字节进度/速度；`src/FilePreview.cs` 快速预览和缓存。
- `src/SplitSettings.cs` 拆分/裁剪参数；`engine/pdf_split.py` 每页/页组 ZIP 与整份 PDF 比例裁剪。
- `src/OfficeConverter.cs` Excel/PPT 独立 COM worker；`src/HtmlConverter.cs` 隔离 Edge/Chrome 打印；`engine/office_formats.py` 新导出、HTML 清理与快速预览。
- `src/PreviewRenderer.cs` 持久管道预览；`engine/preview_worker.py` PDFium 串行渲染/邻页预取；`src/PdfPagePreview.cs` 切割线和半透明灰色遮罩。

## 实际命令
- 开发环境：Python 3.12 创建 `.venv`，`.venv\Scripts\python.exe -m pip install -r engine/requirements.lock`。本轮依赖已安装，保留该环境用于复现，不需重复安装。
- 构建发布：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1`；仅 UI：同命令加 `-GuiOnly`。暂存：`scripts/build-v3.ps1`，发布：`scripts/publish.ps1`。编译器为系统 Framework64 csc，无 NuGet。
- 检查：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1`，正常桌面权限验证 Word；沙箱会拒绝 COM。PDF 与界面流程检查可加 `-SkipWord`；优先测试 .work/release，不存在时从 dist 暂存。
- PDF 检查直接运行：`.venv\Scripts\python.exe tests/pdf_regression.py .work/<检查目录> dist/文件万能处理器/engine/PdfEngine.exe`。
- 可再生内容在 `.work`；清理只对逐项解析、已确认属于本轮的绝对目录操作，不删除 `.venv`、源码、锁、用户备份或其他项目。

## 不可破坏的约束
- 默认只读原件；结果先暂存、后下载，同名自动编号。未完成 `.tmp` 不当结果。
- 下载全程后台执行，准备/落盘阶段保持动画，按总字节报告进度；只有全部原子提交才显示 100%。取消清理未完成文件，保留完整输出和暂存结果；UI 消息完成后必须忽略迟到进度。`tests/DownloadChecks.cs` 验证完整性、取消、批量和界面状态。
- ToolResultStore 按功能隔离结果；未处理功能无下载/旧进度，参数变化必须标明“上次结果”。关闭检查并清理所有功能会话，不能只检查当前页。
- 直接修改仅页面编辑显式选择后确认，检查原件未变化，原子替换并保留同目录 `.bak` 原件；备份属于用户数据，不自动删除。
- 密码不放命令行/设置/正常日志，私有作业目录正常收尾删除。移除加密需正确密码。
- PDFium 只在独立进程内串行使用；预览/缩略图请求可取消，关闭等待收尾。缓存数量有上限。
- 预览进程按窗口复用；不要退回每翻页分别启动 inspect 和 render。worker 用私有文档快照避免锁原件，切文档/密码/文件版本清缓存，最多 20 PNG。比例裁剪灰色为裁去区域，选择保留侧会改变输出参数/结果摘要，但只重画遮罩、不重新渲染。窗口化打包无 sys.stdin/stdout，需要恢复继承的管道句柄。
- Word 宏强制关闭、不更新打开链接，按新建 PID 与启动时间收尾，不批量结束其他 Word。PDF → DOCX 不走易阻塞的 Word PDFREFLOW。
- DOCX 输出为正文重建，扫描页为图片，不承诺 OCR 或原排版。不要把样例通过写成所有格式/机器通过。
- 顶部 FunctionTab 是导航，下面 Toolbar 外观是操作；不要重新混用同一组选中按钮。交付前主动清理本轮暂存、维护 `.gitignore`；当前项目未初始化 Git，不因此擅自 init/提交。
- 导航顺序：转换、页面编辑、合并、拆分、加密、移除加密、压缩；导航和操作项均保留分隔线。每页/页组拆分输出 ZIP；比例裁剪每个输入输出一份 PDF，保留全部页数与顺序，不导出另一半/单页，不打包 ZIP。参数快照在 UI 线程读取；比例裁剪不是脱敏。
- 指定页数拆分每份用两个空数字框“起始页 ~ 结束页”；不要重新要求用户手写范围表达式。UI 校验正整数及起止顺序后生成后端标准范围，实际页数上限仍由后端验证。
- Office 输入仅只读独立进程，不结束用户已有 Excel/PPT；Excel 打开密码占位不得超过 15 字符。HTML 输入仅本地文件，先清理脚本并加 CSP；不要用全局禁用 JavaScript 的浏览器参数（会破坏 CLI 打印）。浏览器独立 profile 与 ProcessJob 收尾，禁止加载远程资源。
- XLSX 提取表格/正文，PPTX 为页面图片，HTML 可选页面图片/正文；必须保留界面与 README 的还原范围说明，不描述为无损/可编辑 PPT。
