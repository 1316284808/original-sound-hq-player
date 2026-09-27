# 详情页封面真实控件回归

先运行相邻的 `SmtcCoverRegression` 生成测试图，再还原本项目依赖并执行 `Run.ps1`。
直接编译生产 ImageSwitcher/ShadowImage XAML、封面缓存、取色、CoverPresentationService、SystemMediaControlsService 和任务停止屏障。
窗口在屏幕外运行，验证三种图片尺寸、快速切歌、隐藏/恢复、过渡层与卸载清理。
通过占用缓存路径和锁住过期缓存阻止发布/替换，验证正方形、纵向及 EXIF 旋转原图的限尺寸解码兜底。
完整展示服务经真实 DispatcherQueue 向真实 SMTC 发布封面，覆盖展示缓存失败转原图、原图写入失败转已有字节、热缓存不重读数组、取消旧请求和退出排空。
`PipelineAdapters.cs` 仅替换歌曲获取、状态、WebDAV 和命令入口，歌曲获取仍使用异步文件读取和生产缓存写入；不启动数据库、网络或音频后端。

这些检查使用真实 WinUI 控件与 DispatcherQueue，但没有启动完整 PlayingDetailPage，也没有验证系统媒体面板的人工视觉效果。
