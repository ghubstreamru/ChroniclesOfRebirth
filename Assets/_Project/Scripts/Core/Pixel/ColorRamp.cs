using UnityEngine;

namespace ChroniclesOfRebirth.Core.Pixel
{
    /// <summary>Упорядоченный набор цветов (от тёмного к светлому) для ограниченной пиксельной палитры.</summary>
    public sealed class ColorRamp
    {
        readonly Color32[] _colors;

        public ColorRamp(params string[] hex)
        {
            _colors = new Color32[hex.Length];
            for (var i = 0; i < hex.Length; i++)
                _colors[i] = Hex(hex[i]);
        }

        public int Count => _colors.Length;

        public Color32 this[int index] => _colors[Mathf.Clamp(index, 0, _colors.Length - 1)];

        /// <summary>Цвет по t в [0, 1] с квантованием на ступени палитры.</summary>
        public Color32 Sample(float t) => this[Mathf.FloorToInt(Mathf.Clamp01(t) * _colors.Length)];

        /// <summary>Цвет по t с упорядоченным дизерингом на границах ступеней.</summary>
        public Color32 SampleDithered(float t, int x, int y, float amount = 1f) =>
            Sample(t + PixelNoise.Bayer(x, y) * amount / _colors.Length);

        /// <summary>Палитра текстурой Count×1 (по пикселю на цвет, Point) — для шейдеров, которые сами выбирают ступень.</summary>
        public Texture2D ToTexture(string name)
        {
            var texture = new Texture2D(_colors.Length, 1, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(_colors);
            texture.Apply(false, false);
            return texture;
        }

        public static Color32 Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out var c)
                ? (Color32)c
                : new Color32(255, 0, 255, 255);
        }
    }
}
