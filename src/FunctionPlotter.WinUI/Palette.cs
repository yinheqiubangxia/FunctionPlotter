using Microsoft.UI.Xaml;
using Windows.UI;

namespace FunctionPlotter.WinUI
{
    /// <summary>
    /// 画布配色。取 WinUI 深色/浅色主题下的标准中性色 + 系统强调色系蓝，
    /// 让自绘图层和 XAML 控件看起来是一套设计语言。
    /// </summary>
    internal readonly struct Palette
    {
        private Palette(Color background, Color grid, Color axis, Color text, Color curve, Color crosshair, Color readoutBackground, Color secondaryText, Color error)
        {
            Background = background;
            Grid = grid;
            Axis = axis;
            Text = text;
            Curve = curve;
            Crosshair = crosshair;
            ReadoutBackground = readoutBackground;
            SecondaryText = secondaryText;
            Error = error;
        }

        public Color Background { get; }

        public Color Grid { get; }

        public Color Axis { get; }

        public Color Text { get; }

        public Color Curve { get; }

        public Color Crosshair { get; }

        public Color ReadoutBackground { get; }

        public Color SecondaryText { get; }

        public Color Error { get; }

        public static Palette For(ElementTheme theme)
        {
            if (theme == ElementTheme.Dark)
            {
                return new Palette(
                    background: Color.FromArgb(0xFF, 0x1B, 0x1B, 0x1B),
                    grid: Color.FromArgb(0xFF, 0x2C, 0x2C, 0x2C),
                    axis: Color.FromArgb(0xFF, 0x5C, 0x5C, 0x5C),
                    text: Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A),
                    curve: Color.FromArgb(0xFF, 0x4C, 0xC2, 0xFF),
                    crosshair: Color.FromArgb(0xFF, 0x6E, 0x6E, 0x6E),
                    readoutBackground: Color.FromArgb(0xF0, 0x2B, 0x2B, 0x2B),
                    secondaryText: Color.FromArgb(0xFF, 0xC8, 0xC8, 0xC8),
                    error: Color.FromArgb(0xFF, 0xFF, 0x99, 0xA4));
            }

            return new Palette(
                background: Color.FromArgb(0xFF, 0xFB, 0xFB, 0xFB),
                grid: Color.FromArgb(0xFF, 0xE9, 0xE9, 0xE9),
                axis: Color.FromArgb(0xFF, 0x99, 0x99, 0x99),
                text: Color.FromArgb(0xFF, 0x6B, 0x6B, 0x6B),
                curve: Color.FromArgb(0xFF, 0x0F, 0x6C, 0xBD),
                crosshair: Color.FromArgb(0xFF, 0x9E, 0x9E, 0x9E),
                readoutBackground: Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF),
                secondaryText: Color.FromArgb(0xFF, 0x61, 0x61, 0x61),
                error: Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C));
        }
    }
}
