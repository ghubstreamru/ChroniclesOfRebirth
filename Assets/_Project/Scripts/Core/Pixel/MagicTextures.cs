using UnityEngine;

namespace ChroniclesOfRebirth.Core.Pixel
{
    /// <summary>Текстуры атмосферы: светящиеся пылинки.</summary>
    public static class MagicTextures
    {
        /// <summary>
        /// Пиксельная искра 8×8: яркое ядро 2×2, короткие лучи-«крестик» и тусклый ореол.
        /// Белая — цвет задаёт частица. Фильтрация Point, как у всей пиксельной графики.
        /// </summary>
        public static Texture2D Mote()
        {
            const int size = 8;
            var canvas = new PixelCanvas(size, size);
            var core = new Color32(255, 255, 255, 255);
            var ray = new Color32(255, 255, 255, 170);
            var halo = new Color32(255, 255, 255, 60);

            for (var y = 1; y < size - 1; y++)
            for (var x = 1; x < size - 1; x++)
            {
                var dx = Mathf.Abs(x - 3.5f);
                var dy = Mathf.Abs(y - 3.5f);
                if (dx < 1f && dy < 1f)
                    canvas.Set(x, y, core);
                else if (dx < 1f || dy < 1f)
                    canvas.Set(x, y, dx + dy < 3f ? ray : halo);
                else if (dx + dy < 3f)
                    canvas.Set(x, y, halo);
            }

            var texture = canvas.ToTexture("Mote", false);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
    }
}
