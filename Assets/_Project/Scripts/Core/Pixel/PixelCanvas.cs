using UnityEngine;

namespace ChroniclesOfRebirth.Core.Pixel
{
    /// <summary>Пиксельный холст в памяти. Начало координат — левый нижний угол, как у Texture2D.</summary>
    public sealed class PixelCanvas
    {
        readonly Color32[] _pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            _pixels = new Color32[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Color32 Get(int x, int y) => _pixels[y * Width + x];

        public void Set(int x, int y, Color32 color)
        {
            if (Contains(x, y))
                _pixels[y * Width + x] = color;
        }

        /// <summary>Запись с переносом через края — для бесшовных текстур.</summary>
        public void SetWrapped(int x, int y, Color32 color)
        {
            x = ((x % Width) + Width) % Width;
            y = ((y % Height) + Height) % Height;
            _pixels[y * Width + x] = color;
        }

        public Color32 GetWrapped(int x, int y)
        {
            x = ((x % Width) + Width) % Width;
            y = ((y % Height) + Height) % Height;
            return _pixels[y * Width + x];
        }

        public void Fill(Color32 color)
        {
            for (var i = 0; i < _pixels.Length; i++)
                _pixels[i] = color;
        }

        /// <summary>Обводит непрозрачные пиксели контуром (4-соседство) цветом outline.</summary>
        public void Outline(Color32 outline)
        {
            var source = (Color32[])_pixels.Clone();
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (source[y * Width + x].a != 0)
                    continue;
                if (IsSolid(source, x - 1, y) || IsSolid(source, x + 1, y) ||
                    IsSolid(source, x, y - 1) || IsSolid(source, x, y + 1))
                    _pixels[y * Width + x] = outline;
            }
        }

        bool IsSolid(Color32[] source, int x, int y) => Contains(x, y) && source[y * Width + x].a != 0;

        /// <summary>Копирует прямоугольник другого холста в позицию (dx, dy); прозрачные пиксели пропускаются.</summary>
        public void Blit(PixelCanvas source, int dx, int dy)
        {
            for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
            {
                var c = source.Get(x, y);
                if (c.a != 0)
                    Set(dx + x, dy + y, c);
            }
        }

        public Texture2D ToTexture(string name, bool repeat)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                anisoLevel = 0,
            };
            texture.SetPixels32(_pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
