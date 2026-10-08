using UnityEngine;

namespace ChroniclesOfRebirth.Core.Pixel
{
    /// <summary>Портальный круг святилища: каменный диск, светящиеся линии и выжженный ореол вокруг.</summary>
    public static class PortalTextures
    {
        /// <summary>Радиусы в пикселях (16 px = 1 юнит).</summary>
        const float DiskRadius = 56f, RimRadius = 58f, ScorchRadius = 77f;

        public static void Generate(int size, int seed, out Texture2D baseMap, out Texture2D emissionMap)
        {
            var baseCanvas = new PixelCanvas(size, size);
            var emission = new PixelCanvas(size, size);
            emission.Fill(new Color32(0, 0, 0, 255));

            var stone = AutumnPalette.PortalStone;
            var scorch = AutumnPalette.Scorch;
            var glow = AutumnPalette.PortalGlow;
            var c = size * 0.5f;

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - c;
                var dy = y + 0.5f - c;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                var angle = Mathf.Atan2(dy, dx);
                if (angle < 0f)
                    angle += Mathf.PI * 2f;

                var edgeNoise = PixelNoise.Fbm(x, y, size, 16, 2, seed) - 0.5f;
                if (d > ScorchRadius + edgeNoise * 12f)
                    continue;

                if (d > RimRadius)
                {
                    // Выжженная трава: плотность падает к краю, переход — дизерингом.
                    var density = 1f - (d - RimRadius) / (ScorchRadius - RimRadius);
                    if (density + PixelNoise.Bayer(x, y) * 0.9f + edgeNoise * 0.6f < 0.35f)
                        continue;
                    baseCanvas.Set(x, y, scorch.SampleDithered(0.2f + density * 0.3f + edgeNoise, x, y));
                    continue;
                }

                if (d > DiskRadius)
                {
                    baseCanvas.Set(x, y, stone[0]);
                    continue;
                }

                // Каменный диск из колец и секторов.
                var ring = d < 20f ? 0 : d < 38f ? 1 : 2;
                var sectors = ring == 0 ? 6 : ring == 1 ? 12 : 18;
                var sectorF = angle / (Mathf.PI * 2f) * sectors;
                var sector = Mathf.FloorToInt(sectorF);
                var nearRingEdge = Mathf.Abs(d - 20f) < 0.8f || Mathf.Abs(d - 38f) < 0.8f;
                var nearSectorEdge = (sectorF - sector) * d * Mathf.PI * 2f / sectors < 1f;
                var tone = PixelNoise.Hash01(ring * 64 + sector, 3, seed);
                var color = nearRingEdge || nearSectorEdge
                    ? stone[0]
                    : stone.SampleDithered(0.3f + tone * 0.4f + edgeNoise * 0.3f, x, y);

                if (IsGlowLine(d, angle, x, y, seed))
                {
                    var g = glow.Sample(0.35f + PixelNoise.Hash01(x, y, seed + 2) * 0.3f);
                    color = g;
                    emission.Set(x, y, g);
                }

                baseCanvas.Set(x, y, color);
            }

            baseMap = baseCanvas.ToTexture("PortalCircle", false);
            emissionMap = emission.ToTexture("PortalCircle_Emission", false);
        }

        static bool IsGlowLine(float d, float angle, int x, int y, int seed)
        {
            // Два кольца и малое внутреннее.
            if (Mathf.Abs(d - 52.5f) < 1.1f || Mathf.Abs(d - 30.5f) < 0.9f || Mathf.Abs(d - 11f) < 0.8f)
                return true;

            // Шесть лучей между кольцами.
            if (d > 30.5f && d < 52.5f)
            {
                var step = Mathf.PI / 3f;
                var a = Mathf.Repeat(angle + step * 0.5f, step) - step * 0.5f;
                if (Mathf.Abs(a) * d < 0.9f)
                    return true;

                // Руны: мелкие знаки 3×3 в поясе между лучами.
                if (d > 40f && d < 44f)
                {
                    var glyph = Mathf.FloorToInt((angle + Mathf.PI * 2f) / (Mathf.PI / 9f));
                    var gx = x & 3;
                    var gy = y & 3;
                    var mask = PixelNoise.Hash(glyph, 0, seed);
                    if (gx < 3 && gy < 3 && ((mask >> (gy * 3 + gx)) & 1) == 1 && Mathf.Abs(a) * d > 3f)
                        return true;
                }
            }

            return false;
        }
    }
}
