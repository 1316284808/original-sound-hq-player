# SMTC 高分辨率封面回归

`dotnet run --project _tools/SmtcCoverRegression/SmtcCoverRegression.csproj -c Release`

直接编译生产 `PlaybackCoverImage`、`ReadOnlyMappedStream` 与 `SystemMediaControlsService`，使用真实 Windows WIC 和 SMTC。
命令/窗口入口使用替身；不播放音频、不启动 WinUI 页面。

验证正方形/非正方形最长边限制、小图原文件复用、JPEG 方向、PNG 透明度、并发生成、取消与重试、损坏源文件、缓存刷新、SMTC 实际传出字节、克隆独立位置、缓存删除、快速切歌、缺失文件与退出句柄释放。
文件读取失败时验证已有字节兜底、可读文件优先、兜底请求取消及退出排空；打开失败通过真实独占文件句柄触发。
通过同一个 1536px 测试文件，对比完整字节数组读取/提交与共享热缓存文件流提交的托管分配、GC 次数及耗时。
此测量仅代表该封面提交路径，不能推断整机 GC 停顿、完整页面或不同文件格式的峰值原生内存。
