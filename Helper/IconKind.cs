namespace WinUIMusicPlayer.Helper;

/// <summary>
/// 字符图标类型枚举。
/// 所有 Segoe Fluent Icons 字形集中由 <see cref="IconService.GetIconChar"/> 映射，
/// XAML 通过 <see cref="IconGlyph.IconKindProperty"/> 附加属性按枚举名引用，避免散落的 \uXXXX 字面量。
/// 命名沿用参考项目 MusicPlayer 的成对约定（Outline=未选中，Filled=选中）。
/// </summary>
public enum IconKind
{
    /// <summary>无图标。</summary>
    None,

    // === 播放控制 ===
    Previous,
    Play,
    Pause,
    Next,
    Lyrics,
    Shuffle,
    RepeatOne,
    RepeatAll,
    PlayModeDefault,
    PlayModeRepeatOff,

    // === 音量 ===
    VolumeHigh,
    VolumeMedium,
    VolumeLow,
    VolumeMin,
    VolumeMute,

    // === 通用 ===
    Add,
    Delete,
    Back,
    Minimize,
    Search,
    Folder,
    FolderOpen,
    NewFolder,
    Settings,
    InSettings,

    // === 收藏（心形，参考项目配对：选中=空心） ===
    Heart,
    HeartFill,

    // === 侧边栏导航（未选中/选中配对） ===
    AllSongsOutline,   // List
    AllSongsFilled,    // InList
    LibraryOutline,    // HeartFill
    LibraryFilled,     // Heart
    ArtistOutline,     // Person
    ArtistFilled,      // InPerson
    AlbumOutline,      // Album
    AlbumFilled,       // InAlbum
    Stats,             // 静态图表图标（未选中态）
    Stats1,            // 静态图表图标（选中态实心）
    SettingsOutline,   // Settings（实心-开）
    SettingsFilled,    // InSettings（空心-关）

    // === 音乐来源（BindUtils.MusicSourceGlyph） ===
    SourceRemote,
    SourceLocal,
    SourceOffline,
    SourceCached,

    // === 全屏 / 锁定 / 桌面歌词 / 逐字 ===
    FullScreenExit,
    FullScreenEnter,
    LockLocked,
    LockUnlocked,
    DesktopLyricsOn,
    DesktopLyricsOff,
    KaraokeOn,
    KaraokeOff,

    // === 存在设备标记（BindUtils.IsExistOnDeviceGlyphConverter） ===
    ExistOnDevice1,
    ExistOnDevice2,

    // === 集中迁移的 XAML 静态字形（按使用处语义命名；含义不确定的用 GlyphExxxx 占位，后续可改名） ===
    Music,              // E93C 专辑/音乐占位音符
    Image,              // E70F 选择封面图片
    Import,             // E72C 扫描/从文件读取
    Save,               // E74E 保存
    More,               // E712 更多（省略号菜单）
    Close,              // E894 关闭（X）
    Warning,            // E946 实验性功能/警告
    Trim,               // E895 修剪
    AddPlaylist,        // E8B5 新建/导入播放列表
    Cloud,              // F385 从网络获取
    Globe,              // F386 网络/地球
    SaveImage,          // E78C 保存图片
    Clock,              // E916 总时长
    Report,             // E787 统计
    Chart,              // EC4A 统计图表
    PlayAll,            // E948 播放全部
    Sort,               // EDE1 排序
    Queue,              // E917 播放队列
    ChevronDown,        // E73E 下拉箭头
    ViewList,           // E76C 列表视图
    OpenFolder,         // F12B 打开文件夹
    NowPlaying,         // E8D6 正在播放详情
    Reset,              // E7A7 重置桌面歌词布局
    Share,              // ECC9 分享/链接
    LyricsAlt,          // EE95 歌词（与 Lyrics 不同字形）
    QueueList,          // E90B 队列列表
    Equalizer,          // E9E9 均衡器
    VolumeAlt,          // E738 音量（与 Volume* 不同字形）
    FullScreenAlt,      // E744 全屏（与 FullScreen* 不同字形）
    GlyphE89F,          // E89F 托盘菜单项（含义待定）
    GlyphE711,          // E711 媒体控制（含义待定）
    GlyphE710,          // E710 媒体控制（含义待定）
    GlyphE70E,          // E70E 媒体控制（含义待定）
    GlyphE70D,          // E70D 媒体控制（含义待定）
    GlyphE71A,          // E71A 媒体控制（含义待定）
    GlyphED3C,          // ED3C 媒体控制（含义待定）
    GlyphED3D,          // ED3D 媒体控制（含义待定）

    // === 主题切换（亮色 F08C / 暗色 F0CE） ===
    LightMode,
    DarkMode,

    // === 关闭导航自定义按钮（参考项目 InHome/BackHome；字形占位，后续按参考项目调整） ===
    InHome,             // TODO: 占位字形，由用户按参考项目调整
    BackHome,           // TODO: 占位字形，由用户按参考项目调整
}
