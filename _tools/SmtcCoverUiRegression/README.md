# 详情页封面真实控件回归

先运行相邻的 `SmtcCoverRegression` 生成测试图，再还原本项目依赖并执行 `Run.ps1`。
直接编译生产 ImageSwitcher/ShadowImage XAML、文件解码与共享高分辨率缓存。
窗口在屏幕外运行，验证三种图片尺寸、快速切歌、隐藏/恢复、过渡层与卸载清理。

这些检查使用真实 WinUI 控件与 DispatcherQueue，但没有启动完整 PlayingDetailPage，也没有验证系统媒体面板的人工视觉效果。
