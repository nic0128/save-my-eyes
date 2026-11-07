using System;
using System.Globalization;
using Windows.UI;

namespace save_my_eyes.Services
{
    public static class ColorUtil
    {
        public static bool TryParse(string? hex, out Color color)
        {
            color = default;

            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            string digits = hex.Trim().TrimStart('#');

            if (digits.Length != 6 ||
                !int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            {
                return false;
            }

            color = Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }

        public static bool IsValidHex(string? hex) => TryParse(hex, out _);

        public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        public static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

        public static bool IsLight(Color color) => (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) > 150;
    }
}
