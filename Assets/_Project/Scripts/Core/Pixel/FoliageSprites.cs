using System.Collections.Generic;
using UnityEngine;

namespace ChroniclesOfRebirth.Core.Pixel
{
    /// <summary>
    /// Атласы спрайтов растительности: «облака» листвы для крон и наземная мелочь.
    /// Плотность <see cref="PixelsPerUnit"/> px/юнит, как у текстур окружения. Формы заданы для эталонных 16 px/юнит
    /// и умножаются на <see cref="K"/>: в мире спрайты прежнего размера, но вдвое подробнее.
    /// </summary>
    public static class FoliageSprites
    {
        public const int PixelsPerUnit = 32;
        const int K = PixelsPerUnit / 16;

        public const int ClumpCell = 32 * K;
        public const int ClumpVariants = 4;
        public const int GroundCell = 16 * K;

        /// <summary>Индексы ячеек наземного атласа.</summary>
        public const int TuftGreen = 0, TuftDry = 1, Flowers = 2, Fern = 3, Ribbons = 4, GlowMushrooms = 5, GroundVariants = 6;

        /// <summary>Атлас из <see cref="ClumpVariants"/> «клубков» листвы, каждый ClumpCell×ClumpCell.</summary>
        public static Texture2D LeafClumpAtlas(ColorRamp ramp, int seed, string name)
        {
            var canvas = new PixelCanvas(ClumpCell * ClumpVariants, ClumpCell);
            var light = new Vector3(-0.45f, 0.65f, 0.6f).normalized;

            for (var v = 0; v < ClumpVariants; v++)
            {
                var vs = seed + v * 131;
                var circles = new List<Vector3>();
                var center = new Vector2(ClumpCell * 0.5f, ClumpCell * 0.47f);
                circles.Add(new Vector3(center.x, center.y, 8.5f * K));
                // Больше мелких «подклубков» по краю — силуэт кроны мягче и пушистее.
                var count = 8 + Mathf.FloorToInt(PixelNoise.Hash01(v, 0, vs) * 4);
                for (var i = 0; i < count; i++)
                {
                    var angle = (i + PixelNoise.Hash01(i, 1, vs) * 0.6f) / count * Mathf.PI * 2f;
                    var dist = (5f + PixelNoise.Hash01(i, 2, vs) * 3.5f) * K;
                    var r = (3.8f + PixelNoise.Hash01(i, 3, vs) * 2.8f) * K;
                    var c = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.85f) * dist;
                    // Клубок должен целиком помещаться в ячейку с запасом 1 px под контур.
                    r = Mathf.Min(r, c.x - 1.5f, ClumpCell - 2.5f - c.x, c.y - 1.5f, ClumpCell - 2.5f - c.y);
                    if (r > 2f * K)
                        circles.Add(new Vector3(c.x, c.y, r));
                }

                var ox = v * ClumpCell;
                var mask = new bool[ClumpCell, ClumpCell];
                var normals = new Vector3[ClumpCell, ClumpCell];

                // 1. Силуэт: объединение кругов с рваным краем; в глубине — тёмная подложка.
                for (var y = 1; y < ClumpCell - 1; y++)
                for (var x = 1; x < ClumpCell - 1; x++)
                {
                    if (!TryLeafNormal(circles, x, y, out var normal))
                        continue;
                    if (normal.z < 0.5f && PixelNoise.Hash01(x, y, vs + 7) > 0.35f + normal.z)
                        continue;

                    mask[x, y] = true;
                    normals[x, y] = normal;
                    var lambert = Mathf.Clamp01(Vector3.Dot(normal, light));
                    canvas.Set(ox + x, y, ramp.Sample(0.05f + lambert * 0.3f));
                }

                // 2. Листья: мелкие штампы 2–3 px, освещённые сверху-слева. Перекрываясь, дают «пиксельную листву».
                var stamps = ClumpCell * ClumpCell / 5;
                for (var s = 0; s < stamps; s++)
                {
                    var x = 1 + Mathf.FloorToInt(PixelNoise.Hash01(s, 0, vs + 3) * (ClumpCell - 2));
                    var y = 1 + Mathf.FloorToInt(PixelNoise.Hash01(s, 1, vs + 3) * (ClumpCell - 2));
                    if (!mask[x, y])
                        continue;

                    var lambert = Mathf.Clamp01(Vector3.Dot(normals[x, y], light));
                    var t = 0.2f + lambert * 0.75f + (PixelNoise.Hash01(s, 2, vs + 3) - 0.5f) * 0.2f;
                    StampLeaf(canvas, mask, ox, x, y, ramp, t, PixelNoise.Hash01(s, 3, vs + 3) > 0.5f);
                }
            }

            // Тёмная кромка только снизу — объём без «мультяшной» обводки.
            ShadeUndersides(canvas, ramp[0]);
            return canvas.ToTexture(name, false);
        }

        /// <summary>Сторона карточки пучка листьев для объёмных крон: 48 px = 1,5 юнита.</summary>
        public const int LeafCardCell = 24 * K;
        public const int LeafCardVariants = 4;

        /// <summary>
        /// Атлас пучков листьев для объёмных крон: на каждой карточке веточки и несколько десятков отдельных листьев.
        /// Лист — две половинки по жилке (освещённая сверху-слева светлее), без обводки; часть листьев — оттенка accent.
        /// Между листьями просветы: крона из таких карточек «дышит», сквозь неё видно ветви и свет.
        /// </summary>
        public static Texture2D LeafCardAtlas(ColorRamp ramp, ColorRamp accent, int seed, string name)
        {
            const int cell = LeafCardCell;
            var canvas = new PixelCanvas(cell * LeafCardVariants, cell);
            var light = new Vector2(-0.6f, 0.8f);
            var twig = AutumnPalette.Bark;

            for (var v = 0; v < LeafCardVariants; v++)
            {
                var vs = seed + v * 173;
                var ox = v * cell;
                var center = new Vector2(cell * 0.5f, cell * 0.48f);

                // 1. Веточки: черенок снизу и отростки веером — видны в просветах между листьями.
                var stemTop = center + new Vector2((PixelNoise.Hash01(v, 0, vs) - 0.5f) * 4f, cell * 0.08f);
                DrawLine(canvas, ox, new Vector2(cell * 0.5f, 1f), stemTop, twig[3]);
                var sprigs = 4 + Mathf.FloorToInt(PixelNoise.Hash01(v, 1, vs) * 3f);
                for (var s = 0; s < sprigs; s++)
                {
                    var angle = (s / (float)sprigs + PixelNoise.Hash01(s, 2, vs) * 0.15f) * Mathf.PI * 2f;
                    var length = cell * (0.25f + PixelNoise.Hash01(s, 3, vs) * 0.15f);
                    DrawLine(canvas, ox, stemTop, stemTop + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length, twig[2]);
                }

                // 2. Листья: дальние (тёмные) первыми, ближние поверх. К краю карточки реже — край рваный.
                const int leaves = 80;
                var order = new List<int>(leaves);
                for (var i = 0; i < leaves; i++)
                    order.Add(i);
                order.Sort((a, b) => PixelNoise.Hash01(a, 4, vs).CompareTo(PixelNoise.Hash01(b, 4, vs)));

                foreach (var i in order)
                {
                    var depth = PixelNoise.Hash01(i, 4, vs);
                    var r = cell * 0.44f * Mathf.Pow(PixelNoise.Hash01(i, 5, vs), 0.6f);
                    var a = PixelNoise.Hash01(i, 6, vs) * Mathf.PI * 2f;
                    var basePoint = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    // Лист растёт наружу от середины пучка, с разбросом.
                    var dirAngle = a + (PixelNoise.Hash01(i, 7, vs) - 0.5f) * 1.6f;
                    var dir = new Vector2(Mathf.Cos(dirAngle), Mathf.Sin(dirAngle));
                    var length = (3f + PixelNoise.Hash01(i, 8, vs) * 1.6f) * K;
                    var width = length * (0.42f + PixelNoise.Hash01(i, 9, vs) * 0.18f);
                    var leafRamp = PixelNoise.Hash01(i, 10, vs) < 0.16f ? accent : ramp;
                    var tone = 0.22f + depth * 0.42f + (PixelNoise.Hash01(i, 11, vs) - 0.5f) * 0.16f + (basePoint.y / cell - 0.5f) * 0.12f;
                    DrawLeaf(canvas, ox, cell, basePoint - dir * length * 0.3f, dir, length, width, light, leafRamp, tone);
                }
            }

            return canvas.ToTexture(name, false);
        }

        static void DrawLeaf(PixelCanvas canvas, int ox, int cell, Vector2 origin, Vector2 dir, float length, float width,
            Vector2 light, ColorRamp ramp, float tone)
        {
            var side = new Vector2(-dir.y, dir.x);
            var litSide = Mathf.Sign(Vector2.Dot(side, light));
            var extent = Mathf.CeilToInt(length + 1f);
            int x0 = Mathf.FloorToInt(origin.x) - extent, y0 = Mathf.FloorToInt(origin.y) - extent;

            for (var y = y0; y <= y0 + extent * 2; y++)
            for (var x = x0; x <= x0 + extent * 2; x++)
            {
                if (x < 1 || y < 1 || x >= cell - 1 || y >= cell - 1)
                    continue;
                var d = new Vector2(x + 0.5f, y + 0.5f) - origin;
                var u = Vector2.Dot(d, dir);
                var w = Vector2.Dot(d, side);
                var f = u / length;
                if (f < 0f || f > 1f)
                    continue;
                // Яйцевидный лист: шире у основания, острый кончик.
                var half = width * 0.5f * Mathf.Sin(Mathf.PI * Mathf.Pow(f, 0.75f));
                if (Mathf.Abs(w) > half)
                    continue;

                var t = tone + (w * litSide > 0f ? 0.12f : -0.06f);
                if (Mathf.Abs(w) < 0.5f && f > 0.12f && f < 0.8f && length > 3f * K)
                    t -= 0.07f; // жилка
                canvas.Set(ox + x, y, ramp.Sample(t));
            }
        }

        static void DrawLine(PixelCanvas canvas, int ox, Vector2 from, Vector2 to, Color32 color)
        {
            var steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 2f);
            for (var s = 0; s <= steps; s++)
            {
                var p = Vector2.Lerp(from, to, s / (float)Mathf.Max(1, steps));
                var x = Mathf.FloorToInt(p.x);
                if (x >= 1 && x < LeafCardCell - 1)
                    canvas.Set(ox + x, Mathf.FloorToInt(p.y), color);
            }
        }

        /// <summary>Ширины ярусов ели в пикселях. Ярус каждой ширины нарисован отдельно — пиксель везде одного размера.</summary>
        public static readonly int[] ConiferTierWidths = { 16 * K, 24 * K, 32 * K, 40 * K, 48 * K, 56 * K, 64 * K, 72 * K };
        public const int ConiferTierVariants = 2;
        const float ConiferTierAspect = 0.62f;

        static int ConiferTierHeight(int width) => Mathf.RoundToInt(width * ConiferTierAspect);

        static int ConiferAtlasWidth
        {
            get
            {
                var sum = 0;
                foreach (var w in ConiferTierWidths)
                    sum += w;
                return sum * ConiferTierVariants;
            }
        }

        static int ConiferAtlasHeight => ConiferTierHeight(ConiferTierWidths[^1]);

        /// <summary>UV-прямоугольник яруса ели заданного класса ширины и его размер в пикселях.</summary>
        public static Rect ConiferTierRect(int widthClass, int variant, out Vector2Int sizePx)
        {
            var x = 0;
            for (var i = 0; i < widthClass; i++)
                x += ConiferTierWidths[i] * ConiferTierVariants;
            var w = ConiferTierWidths[widthClass];
            x += w * (variant % ConiferTierVariants);
            sizePx = new Vector2Int(w, ConiferTierHeight(w));
            float aw = ConiferAtlasWidth, ah = ConiferAtlasHeight;
            return new Rect(x / aw, 0f, w / aw, sizePx.y / ah);
        }

        /// <summary>
        /// Атлас ярусов ели: «юбка» лап с острым верхом, обвисшими кончиками и штрихами хвои.
        /// Свет сверху-слева, низ яруса в тени — ярусы, наложенные друг на друга, читаются как ступенчатый конус.
        /// </summary>
        public static Texture2D ConiferTierAtlas(ColorRamp ramp, int seed, string name)
        {
            var canvas = new PixelCanvas(ConiferAtlasWidth, ConiferAtlasHeight);
            var ox = 0;
            for (var wc = 0; wc < ConiferTierWidths.Length; wc++)
            for (var v = 0; v < ConiferTierVariants; v++)
            {
                var w = ConiferTierWidths[wc];
                DrawConiferTier(canvas, ox, w, ConiferTierHeight(w), ramp, seed + wc * 37 + v * 211);
                ox += w;
            }

            ShadeUndersides(canvas, ramp[0]);
            return canvas.ToTexture(name, false);
        }

        static void DrawConiferTier(PixelCanvas canvas, int ox, int w, int h, ColorRamp ramp, int seed)
        {
            var cx = w * 0.5f;
            var mask = new bool[w, h];
            var top = new int[w];
            var bottom = new int[w];

            for (var x = 0; x < w; x++)
            {
                var dx = Mathf.Abs(x + 0.5f - cx) / cx;
                // Верх: прямой скат к острой макушке, с мелкими «перьями» хвои.
                var feather = Mathf.FloorToInt(PixelNoise.Hash01(x, 0, seed) * 1.7f * K);
                top[x] = Mathf.Clamp(Mathf.RoundToInt((h - 1) - dx * (h - 1) * 0.86f) - feather, 1, h - 1);
                // Низ: лапы провисают к краям, кончики свисают пучками.
                var group = Mathf.FloorToInt((x + PixelNoise.Hash01(0, 1, seed) * 3f * K) / (3f * K));
                var tip = Mathf.RoundToInt(PixelNoise.Hash01(group, 2, seed) * 1.4f * K);
                var droop = Mathf.RoundToInt(h * 0.2f * (1f - dx) - dx * dx * 1.5f * K) - tip;
                bottom[x] = Mathf.Clamp(1 + droop, 1, top[x]);
            }

            for (var x = 0; x < w; x++)
            for (var y = bottom[x]; y <= top[x]; y++)
            {
                mask[x, y] = true;
                var v01 = (y - bottom[x]) / Mathf.Max(1f, top[x] - bottom[x]);
                var side = (cx - (x + 0.5f)) / cx; // слева светлее
                var t = 0.18f + v01 * 0.38f + side * 0.14f + (PixelNoise.Hash01(x, y, seed + 5) - 0.5f) * 0.1f;
                if (y - bottom[x] < 2 * K)
                    t -= 0.12f; // нижняя кромка лап в тени
                canvas.Set(ox + x, y, ramp.Sample(t));
            }

            // Штрихи хвои: короткие светлые черты вниз и наружу от ствола.
            var strokes = w * h / (7 * K);
            for (var s = 0; s < strokes; s++)
            {
                var x = Mathf.FloorToInt(PixelNoise.Hash01(s, 3, seed) * w);
                var y = Mathf.FloorToInt(PixelNoise.Hash01(s, 4, seed) * h);
                if (!mask[x, y])
                    continue;
                var dir = x + 0.5f < cx ? -1 : 1;
                var v01 = (y - bottom[x]) / Mathf.Max(1f, top[x] - bottom[x]);
                var side = (cx - (x + 0.5f)) / cx;
                var t = 0.42f + v01 * 0.35f + side * 0.18f;
                var length = 2 + K + Mathf.FloorToInt(PixelNoise.Hash01(s, 5, seed) * K);
                for (var k = 0; k < length; k++)
                {
                    var px = x + dir * k;
                    var py = y - (k + 1) / 2;
                    if (px < 0 || px >= w || py < 0 || !mask[px, py])
                        break;
                    canvas.Set(ox + px, py, ramp.Sample(t - k * 0.2f / length));
                }
            }

            // Блик по верхнему краю с солнечной стороны.
            for (var x = 0; x < w; x++)
                if (x + 0.5f < cx + 1f && PixelNoise.Hash01(x, 6, seed) > 0.3f)
                    canvas.Set(ox + x, top[x], ramp.Sample(0.8f + (PixelNoise.Hash01(x, 7, seed) - 0.5f) * 0.2f));
        }

        static bool TryLeafNormal(List<Vector3> circles, int x, int y, out Vector3 normal)
        {
            var best = -1f;
            normal = Vector3.zero;
            foreach (var circle in circles)
            {
                var nx = (x + 0.5f - circle.x) / circle.z;
                var ny = (y + 0.5f - circle.y) / circle.z;
                var d2 = nx * nx + ny * ny;
                if (d2 > 1f)
                    continue;
                var nz = Mathf.Sqrt(1f - d2);
                var height = nz * circle.z + circle.y * 0.35f;
                if (height <= best)
                    continue;
                best = height;
                normal = new Vector3(nx, ny, nz);
            }
            return best >= 0f;
        }

        static void StampLeaf(PixelCanvas canvas, bool[,] mask, int ox, int x, int y, ColorRamp ramp, float t, bool wide)
        {
            Put(canvas, mask, ox, x, y, ramp.Sample(t));
            Put(canvas, mask, ox, x, y + 1, ramp.Sample(t + 0.14f));
            Put(canvas, mask, ox, x + 1, y, ramp.Sample(t - 0.08f));
            if (wide)
                Put(canvas, mask, ox, x - 1, y, ramp.Sample(t - 0.04f));
            Put(canvas, mask, ox, x, y - 1, ramp.Sample(t - 0.2f));
        }

        static void Put(PixelCanvas canvas, bool[,] mask, int ox, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= mask.GetLength(0) || y >= mask.GetLength(1) || !mask[x, y])
                return;
            canvas.Set(ox + x, y, color);
        }

        static void ShadeUndersides(PixelCanvas canvas, Color32 shade)
        {
            // Снизу вверх: строка y проверяет ещё не изменённую строку y + 1, без каскада вниз.
            for (var y = 0; y < canvas.Height - 1; y++)
            for (var x = 0; x < canvas.Width; x++)
            {
                if (canvas.Get(x, y).a == 0 && canvas.Get(x, y + 1).a != 0)
                    canvas.Set(x, y, shade);
            }
        }

        /// <summary>Атлас наземной растительности: пучки травы, сухая трава, белые цветы, папоротник.</summary>
        public static Texture2D GroundAtlas(int seed)
        {
            var atlas = new PixelCanvas(GroundCell * GroundVariants, GroundCell);
            atlas.Blit(Tuft(AutumnPalette.Grass, seed), TuftGreen * GroundCell, 0);
            atlas.Blit(Tuft(AutumnPalette.DryGrass, seed + 1), TuftDry * GroundCell, 0);
            atlas.Blit(WhiteFlowers(seed + 2), Flowers * GroundCell, 0);
            atlas.Blit(FernSprite(seed + 3), Fern * GroundCell, 0);
            atlas.Blit(RibbonSprite(seed + 4), Ribbons * GroundCell, 0);
            atlas.Blit(MushroomSprite(seed + 5), GlowMushrooms * GroundCell, 0);
            return atlas.ToTexture("GroundFoliage", false);
        }

        /// <summary>Выцветшие ленты, свисающие с ветки.</summary>
        static PixelCanvas RibbonSprite(int seed)
        {
            var c = new PixelCanvas(GroundCell, GroundCell);
            var wood = AutumnPalette.Bark;
            for (var x = K; x < GroundCell - K; x++)
            for (var t = 0; t < K; t++)
                c.Set(x, 13 * K + t + (x > 9 * K ? 1 : 0), wood[t == K - 1 ? 5 : 3]);

            var ramps = new[] { AutumnPalette.RibbonRed, AutumnPalette.RibbonBlue, AutumnPalette.FlowerWhite };
            for (var i = 0; i < 3; i++)
            {
                var ramp = ramps[i];
                var x = (3 + i * 4) * K;
                var length = (6 + Mathf.FloorToInt(PixelNoise.Hash01(i, 0, seed) * 5)) * K;
                for (var k = 0; k < length; k++)
                {
                    var sway = Mathf.RoundToInt(Mathf.Sin(k * 0.3f + i) * 0.8f * K);
                    var tone = 0.8f - k / (float)length * 0.6f;
                    // Лента шириной в K пикселей: светлая сторона слева, тень справа.
                    for (var w = 0; w < K; w++)
                        c.Set(x + sway + w, 12 * K - k, ramp.Sample(tone - w * 0.2f));
                }
            }

            return c;
        }

        /// <summary>Светящиеся грибы (для эмиссионного материала).</summary>
        static PixelCanvas MushroomSprite(int seed)
        {
            var c = new PixelCanvas(GroundCell, GroundCell);
            var glow = AutumnPalette.Mushroom;
            for (var i = 0; i < 3; i++)
            {
                var x = (4 + i * 4) * K + Mathf.FloorToInt(PixelNoise.Hash01(i, 0, seed) * 2 * K);
                var h = (3 + Mathf.FloorToInt(PixelNoise.Hash01(i, 1, seed) * 4)) * K;
                for (var k = 1; k < h; k++)
                    c.Set(x, k, glow[1]);
                // Шляпка-полусфера: светлая макушка, тёмная кромка.
                var r = 2 * K;
                for (var dy = 0; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                {
                    var d = (dx * dx) / (float)(r * r) + (dy * dy) / (float)(r * r * 0.6f);
                    if (d > 1f)
                        continue;
                    c.Set(x + dx, h + dy, glow.Sample(0.95f - d * 0.5f - dy * 0.05f + dx * 0.02f));
                }
            }

            return c;
        }

        static PixelCanvas Tuft(ColorRamp ramp, int seed)
        {
            var c = new PixelCanvas(GroundCell, GroundCell);
            // Природный пучок: много тонких стеблей разной высоты и наклона, зелёные и сухие вперемешку.
            // Без обводки: у земли стебли темнеют до цвета тени и сливаются с землёй.
            var dry = ramp == AutumnPalette.DryGrass ? AutumnPalette.Grass : AutumnPalette.DryGrass;
            var dryShare = ramp == AutumnPalette.DryGrass ? 0.35f : 0.18f;
            const int blades = 22;
            var order = new List<int>(blades);
            for (var i = 0; i < blades; i++)
                order.Add(i);
            // Дальние (короткие, тёмные) рисуются первыми.
            order.Sort((a, b) => PixelNoise.Hash01(a, 0, seed).CompareTo(PixelNoise.Hash01(b, 0, seed)));

            foreach (var i in order)
            {
                var depth = PixelNoise.Hash01(i, 0, seed); // 0 — дальний, 1 — ближний
                var spread = (PixelNoise.Hash01(i, 1, seed) - 0.5f) * GroundCell * 0.7f;
                var baseX = GroundCell * 0.5f + spread;
                var height = (3.5f + PixelNoise.Hash01(i, 2, seed) * 9f) * K * Mathf.Lerp(0.6f, 1f, depth);
                var lean = (spread / GroundCell * 1.2f + (PixelNoise.Hash01(i, 3, seed) - 0.5f) * 1.6f) * K;
                var blade = PixelNoise.Hash01(i, 4, seed) < dryShare ? dry : ramp;
                var tone = (PixelNoise.Hash01(i, 5, seed) - 0.5f) * 0.18f + (depth - 0.5f) * 0.2f;
                var bent = PixelNoise.Hash01(i, 6, seed) > 0.85f; // надломленный стебель
                for (var k = 0; k < height; k++)
                {
                    var f = k / height;
                    var curve = bent && f > 0.6f ? (f - 0.6f) * 4f * K * Mathf.Sign(lean + 0.01f) : 0f;
                    var x = Mathf.RoundToInt(baseX + lean * f * f * 1.6f + curve);
                    var y = 1 + Mathf.RoundToInt(bent && f > 0.6f ? 0.6f * height + (f - 0.6f) * height * 0.4f : k);
                    c.Set(x, y, blade.Sample(0.12f + 0.62f * f + tone));
                }

                // Колосок у части сухих стеблей.
                if (blade == AutumnPalette.DryGrass && !bent && PixelNoise.Hash01(i, 7, seed) > 0.55f)
                {
                    var tipX = Mathf.RoundToInt(baseX + lean * 1.6f);
                    var tipY = Mathf.RoundToInt(height);
                    for (var e = 0; e < 2 * K; e++)
                    {
                        c.Set(tipX + (e % 2 == 0 ? -1 : 1), tipY - e, blade.Sample(0.75f - e * 0.05f));
                        c.Set(tipX, tipY - e, blade.Sample(0.6f));
                    }
                }
            }

            return c;
        }

        static PixelCanvas WhiteFlowers(int seed)
        {
            var c = new PixelCanvas(GroundCell, GroundCell);
            var stem = AutumnPalette.Grass;
            var petals = AutumnPalette.FlowerWhite;
            var center = AutumnPalette.FlowerCenter;

            for (var i = 0; i < 3; i++)
            {
                var x = (3 + i * 5) * K + Mathf.FloorToInt(PixelNoise.Hash01(i, 0, seed) * 2 * K);
                var h = (5 + Mathf.FloorToInt(PixelNoise.Hash01(i, 1, seed) * 6)) * K;
                var bend = (PixelNoise.Hash01(i, 2, seed) - 0.5f) * 2f;
                for (var k = 1; k < h; k++)
                    c.Set(x + Mathf.RoundToInt(bend * k / h * K), k, stem.Sample(0.3f + k * 0.04f / K));
                // Листики у основания стебля.
                c.Set(x - 1, 2 * K, stem[5]);
                c.Set(x - 2, 2 * K + 1, stem[6]);
                c.Set(x + 1, 3 * K, stem[6]);
                c.Set(x + 2, 3 * K + 1, stem[7]);

                // Цветок из пяти лепестков с жёлтой серединкой; лепестки освещены сверху-слева.
                var fx = x + Mathf.RoundToInt(bend * K);
                for (var dy = -K; dy <= K; dy++)
                for (var dx = -K; dx <= K; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > K)
                        continue;
                    c.Set(fx + dx, h + dy, petals.Sample(0.55f + dy * 0.12f - dx * 0.06f));
                }
                c.Set(fx, h, center[1]);
                if (K > 1)
                    c.Set(fx + 1, h - 1, center[0]);
            }

            return c;
        }

        static PixelCanvas FernSprite(int seed)
        {
            var c = new PixelCanvas(GroundCell, GroundCell);
            var ramp = AutumnPalette.Grass;
            const int fronds = 7;
            for (var i = 0; i < fronds; i++)
            {
                var angle = Mathf.Lerp(22f, 158f, i / (fronds - 1f)) * Mathf.Deg2Rad;
                var length = (6f + PixelNoise.Hash01(i, 0, seed) * 2f) * K;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var side = new Vector2(-dir.y, dir.x);
                for (var s = 0f; s < length; s += 0.5f)
                {
                    var droop = s * s * 0.04f / K;
                    var p = new Vector2(GroundCell * 0.5f, 1f) + dir * s + Vector2.down * droop;
                    var f = s / length;
                    var t = 0.35f + f * 0.5f;
                    c.Set(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), ramp.Sample(t - 0.1f));
                    // Листочки по обе стороны жилки, к кончику короче.
                    if (((int)(s * 2) % 3) == 0 && s > K)
                    {
                        var leaf = (1f - f) * 1.6f * K;
                        for (var l = 1f; l <= leaf; l += 1f)
                        {
                            var a = p + side * l + dir * (l * 0.4f);
                            var b = p - side * l + dir * (l * 0.4f);
                            c.Set(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y), ramp.Sample(t + 0.15f));
                            c.Set(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.y), ramp.Sample(t + 0.05f));
                        }
                    }
                }
            }

            return c;
        }
    }
}
