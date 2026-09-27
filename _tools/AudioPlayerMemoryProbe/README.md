# AudioPlayer 内存采样

诊断程序编译真实 AudioPlayer 源码，保持 NativeAOT、`EventSourceSupport=false`、`SustainedLowLatency`；只替换入口，增加每秒一次托管计数采样和测试专用 GC 请求事件。不替换 `Player/AudioPlayer.exe`，不连接用户正在运行的播放器。SmokeTest 使用独立 IPC scope、静音及真实 WASAPI 共享输出。

从仓库根目录执行（.NET 11 SDK 与现有依赖）：

```powershell
dotnet publish _tools/AudioPlayerMemoryProbe -c Release
./_tools/AudioPlayerMemoryProbe/Create-Fixture.ps1 -Path _tools/AudioPlayerMemoryProbe/bin/reference.wav
dotnet run --project _tools/AudioPlayerSmokeTest -- _tools/AudioPlayerMemoryProbe/bin/Release/net11.0/win-x64/publish/AudioPlayerMemoryProbe.exe _tools/AudioPlayerMemoryProbe/bin/reference.wav 12 --memory --scenario=baseline --vol=0
```

将 `baseline` 分别替换为 `gapless`、`compressor`、`rate025`、`rate5`，各次创建新进程。`switch` 使用 60 秒，每秒在 1× / 1.5× 之间切换。高采样率使用 `-SampleRate 192000 -Seconds 45` 生成另一份文件，跑 baseline / gapless。

- `[memory]` 列：毫秒、累计托管分配字节、`GetTotalMemory(false)`、最近 GC 的 committed 字节、最近 GC 的碎片字节、Gen0/1/2 次数。
- `[memory-process]` 列：阶段、Private Bytes、Working Set、句柄数、线程数。由客户端读取，避免进程枚举开销污染被测托管分配。
- `[memory-phase] stopped` 后保留当前会话以支持重播；约两秒后通过测试专用命名事件执行两次完整 GC，中间等待终结器。此步骤用于区分可回收垃圾和保留对象，**不属于正常播放行为或优化方案**。
- 采样任务自身也分配少量字符串、延迟任务。各组保持相同采样频率；不能把总分配率精确归属到某一生产方法。
- GC 之前的 heap 不等于存活对象量；committed 在首次 GC 前可能为零，不能据此认定没有托管堆。

独立方法测量：

```powershell
dotnet run --project _tools/PlaybackSwitchRegression -- --measure-playback-memory
```

它对真实 FFmpeg 解码循环、倍速循环和压限循环预热后使用当前线程分配计数；卷积构造测量包括准备时的临时分配，不能直接当作长期存活量。它是 JIT 方法实验，进程级矩阵才是 NativeAOT + 真实输出实验。

结果：[分析报告](../../docs/audioplayer-memory-2026-09-27.md)、[数据摘要](Results-2026-09-27.csv)。
