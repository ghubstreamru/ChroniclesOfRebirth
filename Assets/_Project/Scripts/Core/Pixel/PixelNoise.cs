using UnityEngine;

namespace ChroniclesOfRebirth.Core.Pixel
{
    /// <summary>
    /// Детерминированный шум для генерации пиксельных текстур.
    /// Все функции с параметром period бесшовно тайлятся с этим периодом (в клетках решётки).
    /// </summary>
    public static class PixelNoise
    {
        public static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>Псевдослучайное число в [0, 1] для целочисленной точки.</summary>
        public static float Hash01(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / 16777215f;

        static int Wrap(int v, int period) => period <= 0 ? v : ((v % period) + period) % period;

        /// <summary>Сглаженный value noise, бесшовный с периодом period.</summary>
        public static float Value(float x, float y, int period, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = x - x0;
            var fy = y - y0;
            var u = fx * fx * (3f - 2f * fx);
            var v = fy * fy * (3f - 2f * fy);

            var a = Hash01(Wrap(x0, period), Wrap(y0, period), seed);
            var b = Hash01(Wrap(x0 + 1, period), Wrap(y0, period), seed);
            var c = Hash01(Wrap(x0, period), Wrap(y0 + 1, period), seed);
            var d = Hash01(Wrap(x0 + 1, period), Wrap(y0 + 1, period), seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Value noise с разными периодами по осям (для вытянутых узоров: кора, слои земли).</summary>
        public static float ValueAniso(float x, float y, int periodX, int periodY, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = x - x0;
            var fy = y - y0;
            var u = fx * fx * (3f - 2f * fx);
            var v = fy * fy * (3f - 2f * fy);

            var a = Hash01(Wrap(x0, periodX), Wrap(y0, periodY), seed);
            var b = Hash01(Wrap(x0 + 1, periodX), Wrap(y0, periodY), seed);
            var c = Hash01(Wrap(x0, periodX), Wrap(y0 + 1, periodY), seed);
            var d = Hash01(Wrap(x0 + 1, periodX), Wrap(y0 + 1, periodY), seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Фрактальный шум для текстуры size×size с базовым размером ячейки cell пикселей.</summary>
        public static float Fbm(int px, int py, int size, int cell, int octaves, int seed)
        {
            var sum = 0f;
            var norm = 0f;
            var amp = 1f;
            for (var o = 0; o < octaves && cell >= 1; o++)
            {
                var period = Mathf.Max(1, size / cell);
                sum += Value(px / (float)cell, py / (float)cell, period, seed + o * 101) * amp;
                norm += amp;
                amp *= 0.5f;
                cell /= 2;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>
        /// Бесшовный шум Вороного: расстояние до ближайшей и второй точки и id ближайшей ячейки.
        /// cell — размер ячейки в пикселях, size должен делиться на cell.
        /// </summary>
        public static void Voronoi(int px, int py, int size, int cell, float jitter, int seed,
            out float d1, out float d2, out int cellId, out Vector2 offset)
        {
            var cells = Mathf.Max(1, size / cell);
            var cx = px / cell;
            var cy = py / cell;
            d1 = d2 = float.MaxValue;
            cellId = 0;
            offset = Vector2.zero;

            for (var oy = -1; oy <= 1; oy++)
            for (var ox = -1; ox <= 1; ox++)
            {
                var gx = cx + ox;
                var gy = cy + oy;
                var wx = Wrap(gx, cells);
                var wy = Wrap(gy, cells);
                var jx = (Hash01(wx, wy, seed) - 0.5f) * jitter + 0.5f;
                var jy = (Hash01(wx, wy, seed + 17) - 0.5f) * jitter + 0.5f;
                var fx = (gx + jx) * cell;
                var fy = (gy + jy) * cell;
                var dx = px + 0.5f - fx;
                var dy = py + 0.5f - fy;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < d1)
                {
                    d2 = d1;
                    d1 = d;
                    cellId = wy * cells + wx;
                    offset = new Vector2(dx, dy);
                }
                else if (d < d2)
                {
                    d2 = d;
                }
            }
        }

        static readonly float[] Bayer4 =
        {
            0f, 8f, 2f, 10f,
            12f, 4f, 14f, 6f,
            3f, 11f, 1f, 9f,
            15f, 7f, 13f, 5f,
        };

        /// <summary>Порог упорядоченного дизеринга 4×4 в диапазоне [-0.5, 0.5).</summary>
        public static float Bayer(int x, int y) => (Bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f - 0.5f;
    }
}
