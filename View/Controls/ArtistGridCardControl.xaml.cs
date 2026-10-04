using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinUIMusicPlayer.Model;
using WinUIMusicPlayer.ViewModel;

namespace WinUIMusicPlayer.View.Controls;

public sealed partial class ArtistGridCardControl : UserControl
{
    public static readonly DependencyProperty MusicProperty = DependencyProperty.Register(
        nameof(Music), typeof(Music), typeof(ArtistGridCardControl), new PropertyMetadata(null));

    public Music? Music
    {
        get => (Music?)GetValue(MusicProperty);
        set => SetValue(MusicProperty, value);
    }

    private ArtistViewModel ViewModel { get; }

    public ArtistGridCardControl()
    {
        ViewModel = App.Services.GetRequiredService<ArtistViewModel>();
        InitializeComponent();
    }

    private void PlayArtist_Click(object sender, RoutedEventArgs e)
    {
        if (Music is not null)
        {
            _ = ViewModel.PlayArtist(Music);
        }
    }

    /// <summary>阻止按钮点击冒泡成 GridView 的 ItemClick，避免播放的同时又进入歌手详情。</summary>
    private void PlayArtist_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;
}
