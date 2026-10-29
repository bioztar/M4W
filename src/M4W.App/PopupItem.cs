using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using M4W.Core;

namespace M4W.App;

/// <summary>Row view-model for the popup list. Thumbnails are decoded only when the row is actually realised.</summary>
internal sealed class PopupItem
{
    private const int ThumbHeight = 56;

    private readonly Func<long, byte[]?> _loadImage;
    private BitmapSource? _thumbnail;
    private bool _thumbnailTried;

    public PopupItem(ClipEntry entry, Func<long, byte[]?> loadImage)
    {
        Entry = entry;
        _loadImage = loadImage;
    }

    public ClipEntry Entry { get; }
    public string Preview => Entry.Preview;
    public Visibility PinVisibility => Entry.Pinned ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ThumbnailVisibility => Entry.Kind == ClipKind.Image ? Visibility.Visible : Visibility.Collapsed;

    public BitmapSource? Thumbnail
    {
        get
        {
            if (Entry.Kind != ClipKind.Image || _thumbnailTried) return _thumbnail;
            _thumbnailTried = true;
            var png = _loadImage(Entry.Id);
            if (png is null) return null;
            try
            {
                using var ms = new MemoryStream(png);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.DecodePixelHeight = ThumbHeight;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                _thumbnail = bmp;
            }
            catch (Exception ex) when (ex is NotSupportedException or IOException or InvalidOperationException)
            {
                _thumbnail = null; // unreadable image: the row just shows its "Image" label
            }
            return _thumbnail;
        }
    }
}
