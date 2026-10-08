using ChroniclesOfRebirth.Core.Meshes;
using ChroniclesOfRebirth.Core.Pixel;
using UnityEngine;

namespace ChroniclesOfRebirth.Core.World
{
    /// <summary>Процедурные объекты окружения. Всё добавляется в переданные MeshBuilder'ы для объединения в общие меши.</summary>
    public static class PropFactory
    {
        static readonly Color32 White = new(255, 255, 255, 255);

        /// <summary>Размер «клубка» листвы в юнитах: 64 px при 32 px/юнит.</summary>
        public const float ClumpSize = FoliageSprites.ClumpCell / (float)FoliageSprites.PixelsPerUnit;

        /// <summary>
        /// Ель: тонкий ствол и ступенчатый конус из ярусов-«юбок» (атлас <see cref="FoliageSprites.ConiferTierAtlas"/>).
        /// Ярусы идут снизу вверх и сужаются; каждый следующий перекрывает верх предыдущего, а нижняя часть ствола видна.
        /// </summary>
        public static void AddConifer(MeshBuilder trunks, MeshBuilder canopy, Vector3 position, float height, float radius, int seed)
        {
            var ppu = (float)FoliageSprites.PixelsPerUnit;
            var baseY = 0.75f + PixelNoise.Hash01(seed, 0, 5) * 0.3f;

            // Ствол только под нижними ярусами: выше он просвечивал бы сквозь билборды (половина призмы ближе к камере).
            var m = Matrix4x4.TRS(position + Vector3.down * 0.2f, Quaternion.identity, Vector3.one);
            trunks.AddPrism(0, m, 6, 0.24f, 0.16f, baseY + 1f, White, false);

            var crownCenter = position + Vector3.up * (height * 0.55f);
            canopy.Anchor = new Vector4(crownCenter.x, crownCenter.y, crownCenter.z, Mathf.Max(radius, height * 0.5f) + 0.5f);

            var tipWidth = FoliageSprites.ConiferTierWidths[0] / ppu;
            var y = baseY;
            for (var t = 0; t < 32; t++)
            {
                var f = Mathf.Clamp01((y - baseY) / (height - baseY));
                var width = Mathf.Lerp(radius * 2f, tipWidth, Mathf.Pow(f, 0.85f));
                var widthClass = NearestTierClass(width * ppu);
                var rect = FoliageSprites.ConiferTierRect(widthClass, PixelNoise.Hash01(seed, t, 6) > 0.5f ? 1 : 0, out var px);
                var size = new Vector2(px.x, px.y) / ppu;

                // Лёгкий разнобой ярусов, чтобы ель не была идеально симметричной.
                var jitter = new Vector3(PixelNoise.Hash01(seed, t, 7) - 0.5f, 0f, PixelNoise.Hash01(seed, t, 8) - 0.5f) * (size.x * 0.08f);
                var tint = (byte)Mathf.RoundToInt(Mathf.Lerp(175f, 255f, f));
                canopy.AddBillboard(0, position + jitter + Vector3.up * y, size, rect, new Color32(tint, tint, tint, 255));

                if (widthClass == 0 || y + size.y >= height)
                    break;
                y += size.y * 0.5f;
            }

            canopy.Anchor = null;
        }

        static int NearestTierClass(float widthPx)
        {
            var best = 0;
            for (var i = 1; i < FoliageSprites.ConiferTierWidths.Length; i++)
                if (Mathf.Abs(FoliageSprites.ConiferTierWidths[i] - widthPx) < Mathf.Abs(FoliageSprites.ConiferTierWidths[best] - widthPx))
                    best = i;
            return best;
        }

        /// <summary>Куст: 2–4 клубка листвы у самой земли.</summary>
        public static void AddBush(MeshBuilder canopy, Vector3 position, int seed)
        {
            canopy.Anchor = new Vector4(position.x, position.y + 0.8f, position.z, 1.6f);
            var count = 2 + Mathf.FloorToInt(PixelNoise.Hash01(seed, 0, 15) * 3f);
            for (var i = 0; i < count; i++)
            {
                var offset = new Vector3(PixelNoise.Hash01(seed, i, 16) - 0.5f, 0f, PixelNoise.Hash01(seed, i, 17) - 0.5f) * 1.4f;
                var pivot = position + offset + Vector3.up * (0.5f + PixelNoise.Hash01(seed, i, 18) * 0.4f);
                AddClump(canopy, pivot, 0.55f + PixelNoise.Hash01(seed, i, 19) * 0.4f, seed + i * 7);
            }

            canopy.Anchor = null;
        }

        static void AddClump(MeshBuilder canopy, Vector3 pivot, float brightness01, int seed)
        {
            var variant = Mathf.FloorToInt(PixelNoise.Hash01(seed, 0, 21) * FoliageSprites.ClumpVariants) % FoliageSprites.ClumpVariants;
            var w = 1f / FoliageSprites.ClumpVariants;
            var rect = new Rect(variant * w, 0f, w, 1f);
            var tint = (byte)Mathf.Clamp(Mathf.Lerp(150f, 255f, brightness01), 0f, 255f);
            canopy.AddBillboard(0, pivot, new Vector2(ClumpSize, ClumpSize), rect, new Color32(tint, tint, tint, 255), 0.5f);
        }

        /// <summary>Колонна святилища: база, ствол и (если цела) капитель. brokenHeight &gt; 0 — обломанный верх.</summary>
        public static void AddColumn(MeshBuilder stone, Vector3 position, float height, bool intact, int seed)
        {
            stone.AddBox(0, Matrix4x4.TRS(position + Vector3.up * 0.15f, Quaternion.identity, Vector3.one), new Vector3(1.15f, 0.5f, 1.15f), White);
            var shaftMatrix = Matrix4x4.TRS(position + Vector3.up * 0.4f, Quaternion.Euler(0f, 22.5f, 0f), Vector3.one);

            if (intact)
            {
                stone.AddPrism(0, shaftMatrix, 8, 0.42f, 0.38f, height, White);
                stone.AddBox(0, Matrix4x4.TRS(position + Vector3.up * (0.4f + height + 0.15f), Quaternion.identity, Vector3.one),
                    new Vector3(1.05f, 0.3f, 1.05f), White);
                return;
            }

            var jitter = new float[8];
            for (var i = 0; i < jitter.Length; i++)
                jitter[i] = -PixelNoise.Hash01(seed, i, 9) * 0.45f;
            stone.AddPrism(0, shaftMatrix, 8, 0.42f, 0.4f, height, White, true, jitter);
        }

        /// <summary>Упавшая колонна: лежащий ствол на боку.</summary>
        public static void AddFallenColumn(MeshBuilder stone, Vector3 position, float length, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 90f);
            var m = Matrix4x4.TRS(position + Vector3.up * 0.32f, rotation, Vector3.one);
            stone.AddPrism(0, m, 8, 0.42f, 0.4f, length, White);
        }
    }
}
