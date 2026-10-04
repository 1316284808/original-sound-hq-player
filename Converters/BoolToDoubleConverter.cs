using Microsoft.UI.Xaml.Data;
using System;

namespace WinUIMusicPlayer.Converters
{
    /// <summary>
    /// 布尔值 → 宽度：true 返回展开宽度（默认 220，可用 parameter 形如 "200" 覆盖），false 返回 0。
    /// </summary>
    public partial class BoolToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double expanded = 220;
            if (parameter is string p && double.TryParse(p, out var parsed))
            {
                expanded = parsed;
            }
            if (value is bool b && b)
            {
                return expanded;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
