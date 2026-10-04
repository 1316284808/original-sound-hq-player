namespace WinUIMusicPlayer.Helper;

/// <summary>
/// 图标服务：集中管理所有 Segoe Fluent Icons 字符字形，作为全局唯一真源。
/// 所有 <c>\uXXXX</c> 字面量只应出现在此处，XAML/代码通过 <see cref="IconKind"/> 引用。
/// </summary>
public static class IconService
{
    /// <summary>获取图标对应的 Segoe Fluent Icons Unicode 字符。</summary>
    /// <param name="kind">图标类型。</param>
    /// <returns>对应的 Unicode 字符；未定义时返回空字符串。</returns>
    public static string GetIconChar(IconKind kind) => kind switch
    {
        // 播放控制
        IconKind.Previous => "\uf8ac",
        IconKind.Play => "\uF5B0",
        IconKind.Pause => "\uF8AE",
        IconKind.Next => "\uf8ad",
        IconKind.Lyrics => "\uf4a5",
        IconKind.Shuffle => "\uE8B1",
        IconKind.RepeatOne => "\uE8ED",
        IconKind.RepeatAll => "\uE8EE",
        IconKind.PlayModeDefault => "\uE8AB",
        IconKind.PlayModeRepeatOff => "\uF5E7",

        // 音量
        IconKind.VolumeHigh => "\uE995",
        IconKind.VolumeMedium => "\uE994",
        IconKind.VolumeLow => "\uE993",
        IconKind.VolumeMin => "\uE992",
        IconKind.VolumeMute => "\uE74F",

        // 通用
        IconKind.Add => "\uf8aa",
        IconKind.Delete => "\ue74d",
        IconKind.Back => "\uf0d5",
        IconKind.Search => "\uf78b",
        IconKind.Minimize => "\uE949",   // ChromeMinimize 最小化
        IconKind.Folder => "\uE8B7",
        IconKind.FolderOpen => "\ue838",
        IconKind.NewFolder => "\ue8f4",
        IconKind.Settings => "\uf8b0",
        IconKind.InSettings => "\ue713",

        // 收藏
        IconKind.Heart => "\uEB51",
        IconKind.HeartFill => "\uEB52",

        // 侧边栏导航（未选中/选中配对，沿用参考项目 MusicPlayer 的字形方向）
        IconKind.AllSongsOutline => "\uE8D5",   // List
        IconKind.AllSongsFilled => "\uE8B7",    // InList
        IconKind.LibraryOutline => "\uEB52",    // HeartFill
        IconKind.LibraryFilled => "\uEB51",     // Heart
        IconKind.ArtistOutline => "\uEA8C",     // Person
        IconKind.ArtistFilled => "\uE77B",      // InPerson
        IconKind.AlbumOutline => "\uE735",      // Album
        IconKind.AlbumFilled => "\uE734",       // InAlbum
        IconKind.Stats => "\ued0c",             // 静态图表图标
        IconKind.Stats1=> "\ued0d",
        IconKind.SettingsOutline => "\uf8b0",   // Settings（实心-开）
        IconKind.SettingsFilled => "\ue713",    // InSettings（空心-关）

        // 音乐来源（BindUtils.MusicSourceGlyph）
        IconKind.SourceRemote => "\uE753",
        IconKind.SourceLocal => "\uE8B7",
        IconKind.SourceOffline => "\uF384",
        IconKind.SourceCached => "\uEBD3",

        // 全屏 / 锁定 / 桌面歌词 / 逐字
        IconKind.FullScreenExit => "\uE73F",
        IconKind.FullScreenEnter => "\uE740",
        IconKind.LockLocked => "\uE72E",
        IconKind.LockUnlocked => "\uE785",
        IconKind.DesktopLyricsOn => "\uE890",
        IconKind.DesktopLyricsOff => "\uED1A",
        IconKind.KaraokeOn => "\uE73A",
        IconKind.KaraokeOff => "\uE739",

        // 存在设备标记（BindUtils.IsExistOnDeviceGlyphConverter）
        IconKind.ExistOnDevice1 => "\uE73A",
        IconKind.ExistOnDevice2 => "\uE73D",

        // 集中迁移的 XAML 静态字形
        IconKind.Music => "\uE93C",
        IconKind.Image => "\uE70F",
        IconKind.Import => "\uE72C",
        IconKind.Save => "\uE74E",
        IconKind.More => "\uE712",
        IconKind.Close => "\uE894",
        IconKind.Warning => "\uE946",
        IconKind.Trim => "\uE895",
        IconKind.AddPlaylist => "\uE8B5",
        IconKind.Cloud => "\uF385",
        IconKind.Globe => "\uF386",
        IconKind.SaveImage => "\uE78C",
        IconKind.Clock => "\uE916",
        IconKind.Report => "\uE787",
        IconKind.Chart => "\uEC4A",
        IconKind.PlayAll => "\uE948",
        IconKind.Sort => "\uEDE1",
        IconKind.Queue => "\uE917",
        IconKind.ChevronDown => "\uE73E",
        IconKind.ViewList => "\uE76C",
        IconKind.OpenFolder => "\uF12B",
        IconKind.NowPlaying => "\uE8D6",
        IconKind.Reset => "\uE7A7",
        IconKind.Share => "\uECC9",
        IconKind.LyricsAlt => "\uEE95",
        IconKind.QueueList => "\uE90B",
        IconKind.Equalizer => "\uE9E9",
        IconKind.VolumeAlt => "\uE738",
        IconKind.FullScreenAlt => "\uE744",
        IconKind.GlyphE89F => "\uE89F",
        IconKind.GlyphE711 => "\uE711",
        IconKind.GlyphE710 => "\uE710",
        IconKind.GlyphE70E => "\uE70E",
        IconKind.GlyphE70D => "\uE70D",
        IconKind.GlyphE71A => "\uE71A",
        IconKind.GlyphED3C => "\uED3C",
        IconKind.GlyphED3D => "\uED3D",

        // 主题切换
        IconKind.DarkMode => "\uF08C",
        IconKind. LightMode => "\uf4a5",

        IconKind.BackHome => "\uea8a",// 空心小房子-开
        IconKind.InHome => "\ue80f",//实心小房子 -关

        _ => "",
    };
}
