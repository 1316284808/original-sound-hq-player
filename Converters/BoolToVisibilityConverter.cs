using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace WinUIMusicPlayer.Converters
{
    /// <summary>
    /// 布尔值 → Visibility：true 显示，false 折叠。parameter="Invert" 时反转。
    /// </summary>
    public partial class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool invert = parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase);
            bool flag = value is bool b && b;
            if (invert) flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility v)
            {
                bool visible = v == Visibility.Visible;
                if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                    visible = !visible;
                return visible;
            }
            return false;
        }
    }
}
