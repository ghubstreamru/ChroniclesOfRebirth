using UnityEngine;

namespace ChroniclesOfRebirth.Core.Pixel
{
    /// <summary>
    /// Спрайт-лист главного героя. Формы заданы в «проектных» пикселях (36×56 на кадр, 32 px/юнит) и растеризуются
    /// с детализацией <see cref="Detail"/>: плавные контуры, тонкие черты лица, пряди и складки. Размер в мире —
    /// 1,125×1,75 юнита. 4 кадра в ряду (стоит, шаг, проход, шаг), ряды сверху вниз: вниз (к камере), влево, вправо,
    /// вверх (от камеры). У каждой части своя палитра, объём от света сверху-слева; контур не чёрный — край силуэта
    /// темнеет в тень своей же части.
    /// </summary>
    public static class HeroSpriteSheet
    {
        /// <summary>Во сколько раз спрайт подробнее мира (мир — 32 px/юнит).</summary>
        public const int Detail = 2;
        public const int PixelsPerUnit = 32 * Detail;
        const int DesignWidth = 36, DesignHeight = 56;
        public const int FrameWidth = DesignWidth * Detail;
        public const int FrameHeight = DesignHeight * Detail;
        public const int Frames = 4;
        public const int Directions = 4;

        public const int RowDown = 0, RowLeft = 1, RowRight = 2, RowUp = 3;

        // Палитры частей: от тени к свету, тени уходят в холодный, света — в тёплый.
        static readonly ColorRamp Hair = new("22120a", "33190e", "4a2515", "62321b", "7c4223", "97552d", "b06a39", "c7834d", "d89d66");
        static readonly ColorRamp Skin = new("5e3426", "7a4634", "985a45", "b37159", "c9886d", "da9f84", "e8b69b", "f3cbb2");
        static readonly ColorRamp Tunic = new("0f1f30", "152a42", "1c3654", "234467", "2c5379", "37638b", "45749c", "5888ae", "6f9dc0");
        static readonly ColorRamp Cloak = new("0f121a", "151a25", "1c2231", "242c3e", "2e374c", "39445b", "46526b");
        static readonly ColorRamp Pants = new("221d1a", "2e2824", "3b342f", "4a423c", "5a514a", "6b6159", "7d7269");
        static readonly ColorRamp Boots = new("160e09", "22150d", "301e13", "40291a", "523623", "66452e", "7b573a");
        static readonly ColorRamp Leather = new("1d120b", "2c1c12", "3e2819", "523521", "66452c");
        static readonly ColorRamp Gold = new("6e521c", "9c7a2e", "c9a243", "e8c870");
        static readonly ColorRamp GlyphGlow = new("3b8fb5", "6cc3ef", "b8ecff");
        static readonly ColorRamp Iris = new("1d1410", "3b2a1e", "5a4330");
        static readonly Color32 EyeDark = ColorRamp.Hex("150e10");
        static readonly Color32 EyeWhite = ColorRamp.Hex("e9e2d8");
        static readonly Color32 EyeLight = ColorRamp.Hex("fbf7ef");
        static readonly Vector3 Light = new Vector3(-0.5f, 0.6f, 0.62f).normalized;

        /// <summary>Ось симметрии (проектные пиксели): между 17 и 18.</summary>
        const float Mid = DesignWidth / 2f;
        static int L(int a) => DesignWidth / 2 - 1 - a;
        static int R(int a) => DesignWidth / 2 + a;

        public static Texture2D Generate()
        {
            var sheet = new PixelCanvas(FrameWidth * Frames, FrameHeight * Directions);
            for (var row = 0; row < Directions; row++)
            for (var frame = 0; frame < Frames; frame++)
            {
                var canvas = DrawFrame(row, frame);
                sheet.Blit(canvas, frame * FrameWidth, (Directions - 1 - row) * FrameHeight);
            }

            return sheet.ToTexture("Hero", false);
        }

        static PixelCanvas DrawFrame(int row, int frame)
        {
            var f = new Frame(DesignWidth, DesignHeight, Detail);
            // Кадры 1 и 3 — шаг (ноги расходятся, корпус на пиксель ниже), 0 и 2 — ноги вместе.
            var step = frame == 1 ? 1 : frame == 3 ? -1 : 0;
            var bob = step != 0 ? -1 : 0;

            switch (row)
            {
                case RowDown: DrawFront(f, step, bob); break;
                case RowUp: DrawBack(f, step, bob); break;
                default: DrawSide(f, step, bob, frame); break;
            }

            var canvas = f.Resolve();
            if (row == RowLeft)
                Mirror(canvas);
            return canvas;
        }

        // ---------- Вид спереди ----------

        static void DrawFront(Frame f, int step, int bob)
        {
            // Плащ за спиной виден по бокам.
            f.Trapezoid(L(9), R(9), 33.5f + bob, L(8), R(8), 17 + bob, Cloak, 0.3f);

            // Ноги: на шаге одна нога приподнята (стопа выше), другая стоит.
            Leg(f, L(4), L(1), step > 0 ? 2 : 0, bob);
            Leg(f, R(1), R(4), step < 0 ? 2 : 0, bob);

            Torso(f, bob, true);

            // Руки качаются в противофазе ногам; рукава темнее туники, сторона к телу в тени.
            Arm(f, L(10), L(8), 33 + bob, step > 0 ? -1 : step < 0 ? 1 : 0, true);
            Arm(f, R(8), R(10), 33 + bob, step < 0 ? -1 : step > 0 ? 1 : 0, false);

            // Застёжка плаща на плече.
            f.Disc(L(6) + 0.5f, 33.6f + bob, 0.7f, Gold, 0.7f);

            FrontHead(f, bob);
        }

        static void Torso(Frame f, int bob, bool front)
        {
            // Туника: плечи шире пояса, ниже пояса короткий подол с тёмной кромкой.
            f.Trapezoid(L(7), R(7), 33.5f + bob, L(5), R(5), 21 + bob, Tunic, 0.55f);
            f.Trapezoid(L(5), R(5), 20.5f + bob, L(6), R(6), 15 + bob, Tunic, 0.42f);
            f.Band(L(6), R(6), 15f + bob, 0.5f, Tunic, 0.12f);
            // Складки ткани: тонкие тени от пояса вниз и от плеч к груди.
            f.Fold(Mid - 3.5f, 20f + bob, Mid - 4.5f, 15.5f + bob, Tunic, 0.25f);
            f.Fold(Mid + 2.5f, 20f + bob, Mid + 3.5f, 15.5f + bob, Tunic, 0.22f);
            f.Fold(Mid - 5.5f, 32f + bob, Mid - 3.5f, 26f + bob, Tunic, 0.35f);
            if (front)
            {
                // V-образный вырез и знак на груди.
                f.Triangle(Mid - 2.5f, 33.6f + bob, Mid + 2.5f, 33.6f + bob, Mid, 30.2f + bob, Skin, 0.32f);
                f.Glyph(Mid, 27.5f + bob);
            }
            Belt(f, L(5), R(5), 20 + bob, front);
        }

        static void FrontHead(Frame f, int bob)
        {
            var cy = 43.5f + bob;
            f.Rect(L(1), 34 + bob, R(1), 36 + bob, Skin, 0.22f); // шея в тени
            f.Ellipse(Mid, cy, 7.6f, 8.6f, Skin, 0.12f);
            // Подбородок и скулы мягче: тень под щеками.
            f.Fold(Mid - 5.5f, cy - 4f, Mid - 3f, cy - 7.2f, Skin, 0.32f);
            f.Fold(Mid + 5.5f, cy - 4f, Mid + 3f, cy - 7.2f, Skin, 0.28f);

            // Волосы: объёмная шапка, по бокам закрывают уши; чёлка прядями; на всём — штрихи прядей.
            f.EllipseWhere(Mid, 46.5f + bob, 9.2f, 9f, Hair, (x, y) => y >= 46f + bob || x <= Mid - 6.6f || x >= Mid + 6.6f);
            Spikes(f, Mid, 46.5f + bob, 9.2f, 9f, 3);
            Fringe(f, Mid - 6.8f, Mid + 6.8f, 46.2f + bob, 2.4f, 3);
            Strands(f, Mid, 46.5f + bob, 9.2f, 9f, 5);

            // Глаза: ресницы, белок, радужка с бликом. Брови. Нос, рот, румянец.
            Eye(f, Mid - 3.2f, 42.2f + bob, false);
            Eye(f, Mid + 3.2f, 42.2f + bob, true);
            f.Stroke(Mid - 4.6f, 45.2f + bob, Mid - 1.9f, 45.6f + bob, Hair, 0.12f);
            f.Stroke(Mid + 1.9f, 45.6f + bob, Mid + 4.6f, 45.2f + bob, Hair, 0.12f);
            f.Stroke(Mid + 0.3f, 41.2f + bob, Mid + 0.8f, 39.4f + bob, Skin, 0.38f); // тень носа справа
            f.Fine(Mid - 0.2f, 39.2f + bob, Skin, 0.78f);                              // свет на кончике носа
            f.Stroke(Mid - 1.1f, 37.6f + bob, Mid + 1.1f, 37.6f + bob, Skin, 0.22f);   // рот
            f.Disc(Mid - 5f, 39.6f + bob, 0.9f, Skin, 0.62f);                          // тёплый свет на щеке
        }

        static void Eye(Frame f, float cx, float cy, bool right)
        {
            // Белок и радужка — тонкие, в долях проектного пикселя.
            f.FineRect(cx - 1.1f, cy - 0.6f, cx + 1.1f, cy + 0.6f, EyeWhite);
            f.FineRect(cx - 0.6f, cy - 0.9f, cx + 0.5f, cy + 0.8f, Iris.Sample(0.5f));
            f.FineRect(cx - 0.3f, cy - 0.6f, cx + 0.2f, cy + 0.3f, EyeDark);
            f.FineRect(right ? cx - 0.5f : cx - 0.6f, cy + 0.1f, right ? cx - 0.1f : cx - 0.2f, cy + 0.5f, EyeLight);
            f.FineRect(cx - 1.3f, cy + 0.7f, cx + 1.3f, cy + 1.1f, EyeDark); // ресницы
        }

        // ---------- Вид со спины ----------

        static void DrawBack(Frame f, int step, int bob)
        {
            Leg(f, L(4), L(1), step > 0 ? 2 : 0, bob);
            Leg(f, R(1), R(4), step < 0 ? 2 : 0, bob);
            Torso(f, bob, false);
            Arm(f, L(10), L(8), 33 + bob, step < 0 ? -1 : step > 0 ? 1 : 0, true);
            Arm(f, R(8), R(10), 33 + bob, step > 0 ? -1 : step < 0 ? 1 : 0, false);

            // Плащ закрывает спину: расширяется книзу, складки — тени, подол неровный.
            f.Trapezoid(L(8), R(8), 35.5f + bob, L(9), R(9), 16.5f + bob, Cloak, 0.5f);
            f.Fold(Mid - 4f, 31f + bob, Mid - 5f, 17.5f + bob, Cloak, 0.18f);
            f.Fold(Mid + 3f, 31f + bob, Mid + 4.5f, 17.5f + bob, Cloak, 0.2f);
            f.Fold(Mid - 0.5f, 28f + bob, Mid - 0.5f, 17.5f + bob, Cloak, 0.28f);
            f.Hem(L(9), R(9), 16.5f + bob, 3);

            f.Rect(L(1), 34 + bob, R(1), 36 + bob, Skin, 0.2f);
            f.Ellipse(Mid, 45f + bob, 8.8f, 9.6f, Hair, 0.15f);
            Spikes(f, Mid, 45f + bob, 8.8f, 9.6f, 4);
            Strands(f, Mid, 45f + bob, 8.8f, 9.6f, 6);
            // Затылок: пряди вниз, к шее.
            for (var i = 0; i < 9; i++)
            {
                var x = Mid - 5f + i * 1.25f;
                f.Stroke(x, 39.5f + bob, x + (i - 4) * 0.15f, 36.2f + bob - (i % 2) * 0.6f, Hair, 0.3f + (x < Mid ? 0.1f : 0f));
            }
        }

        // ---------- Вид сбоку (вправо; влево — отражение) ----------

        static void DrawSide(Frame f, int step, int bob, int frame)
        {
            const int cx = DesignWidth / 2;
            var flutter = frame % 2 == 0 ? 0f : 0.8f;

            // Плащ за спиной, слегка развевается при ходьбе.
            f.Quad(cx - 5, 35.5f + bob, cx + 1, 35.5f + bob, cx - 2, 17 + bob, cx - 9 - flutter, 17 + bob, Cloak, 0.35f);
            f.Fold(cx - 3f, 33f + bob, cx - 6.5f - flutter, 18f + bob, Cloak, 0.18f);

            // Дальние нога и рука темнее, ближние светлее; на шаге стопы расходятся.
            SideLeg(f, cx - 1, -step * 3, bob, 0.3f);
            SideArm(f, cx - 1, 33 + bob, step * 2, 0.22f);
            SideLeg(f, cx + 1, step * 3, bob, 0.6f);

            f.Trapezoid(cx - 4, cx + 4, 33.5f + bob, cx - 3, cx + 4, 21 + bob, Tunic, 0.55f);
            f.Trapezoid(cx - 3, cx + 4, 20.5f + bob, cx - 4, cx + 5, 15 + bob, Tunic, 0.42f);
            f.Band(cx - 4, cx + 5, 15f + bob, 0.5f, Tunic, 0.12f);
            Belt(f, cx - 3, cx + 4, 20 + bob, false);
            f.Glyph(cx + 2.5f, 27.5f + bob);

            SideArm(f, cx, 33 + bob, -step * 2, 0.38f); // ближняя рука

            f.Rect(cx - 1, 34 + bob, cx + 2, 36 + bob, Skin, 0.22f);
            var hy = 43.5f + bob;
            f.Ellipse(cx + 0.5f, hy, 8f, 8.6f, Skin, 0.12f);
            f.Fold(cx + 6f, hy - 3.5f, cx + 3.5f, hy - 7.2f, Skin, 0.3f); // линия челюсти
            // Волосы: затылок и макушка, лицо открыто спереди.
            f.EllipseWhere(cx - 0.5f, 46f + bob, 8.8f, 9.4f, Hair, (x, y) => x <= cx + 0.5f || y >= 46f + bob);
            Spikes(f, cx - 1.5f, 46f + bob, 8.8f, 9.4f, 3);
            Fringe(f, cx + 0.5f, cx + 6.8f, 46f + bob, 2f, 7);
            Strands(f, cx - 0.5f, 46f + bob, 8.8f, 9.4f, 8);
            f.Disc(cx + 0.6f, 42f + bob, 0.9f, Skin, 0.45f); // ухо

            f.Stroke(cx + 3.6f, 45.4f + bob, cx + 6.2f, 45.1f + bob, Hair, 0.12f); // бровь
            f.FineRect(cx + 4.2f, hy - 1.9f, cx + 5.6f, hy - 0.7f, EyeWhite);
            f.FineRect(cx + 4.7f, hy - 2.1f, cx + 5.6f, hy - 0.5f, Iris.Sample(0.5f));
            f.FineRect(cx + 5f, hy - 1.8f, cx + 5.5f, hy - 0.9f, EyeDark);
            f.FineRect(cx + 4.9f, hy - 1.2f, cx + 5.2f, hy - 0.8f, EyeLight);
            f.FineRect(cx + 4f, hy - 0.6f, cx + 5.8f, hy - 0.2f, EyeDark);  // ресницы
            f.Disc(cx + 8f, hy - 3.6f, 0.75f, Skin, 0.7f);                 // кончик носа
            f.Stroke(cx + 5.6f, hy - 5.9f, cx + 6.8f, hy - 5.9f, Skin, 0.22f); // рот
        }

        // ---------- Части ----------

        /// <summary>Пряди на макушке: заострённые выступы над верхним краем головы.</summary>
        static void Spikes(Frame f, float cx, float cy, float rx, float ry, int seed)
        {
            for (var i = 0; i < 7; i++)
            {
                var u = -0.75f + i * 0.25f + (PixelNoise.Hash01(i, 0, seed) - 0.5f) * 0.1f;
                var top = cy + ry * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
                var height = 0.8f + PixelNoise.Hash01(i, 1, seed) * 1.6f;
                var lean = (u * 1.2f + (PixelNoise.Hash01(i, 2, seed) - 0.5f)) * 1.2f;
                var bx = cx + u * rx;
                f.Triangle(bx - 1.1f, top - 0.8f, bx + 1.1f, top - 0.8f, bx + lean, top + height, Hair, 0.55f + (u < 0 ? 0.15f : 0f));
            }
        }

        /// <summary>Чёлка: рваные пряди, свисающие на лоб; светлее слева.</summary>
        static void Fringe(Frame f, float x0, float x1, float top, float length, int seed)
        {
            var count = Mathf.RoundToInt((x1 - x0) / 1.6f);
            for (var i = 0; i <= count; i++)
            {
                var x = Mathf.Lerp(x0, x1, i / (float)count);
                var l = length * (0.6f + PixelNoise.Hash01(i, 0, seed) * 0.7f);
                f.Triangle(x - 1f, top, x + 1f, top, x + 0.6f, top - l, Hair, 0.5f + (x < Mid ? 0.12f : 0f));
            }
        }

        /// <summary>Штрихи прядей поверх волос: тёмные и светлые линии от макушки вниз и в стороны.</summary>
        static void Strands(Frame f, float cx, float cy, float rx, float ry, int seed)
        {
            for (var i = 0; i < 14; i++)
            {
                var a = Mathf.Lerp(-2.4f, 2.4f, i / 13f) + (PixelNoise.Hash01(i, 0, seed) - 0.5f) * 0.25f;
                var start = new Vector2(cx + Mathf.Sin(a) * rx * 0.25f, cy + ry * 0.75f);
                var end = new Vector2(cx + Mathf.Sin(a) * rx * 0.95f, cy + Mathf.Cos(a) * ry * 0.95f);
                var tone = PixelNoise.Hash01(i, 1, seed) > 0.5f ? 0.72f : 0.25f;
                f.StrokeInside(start.x, start.y, end.x, end.y, Hair, tone + (end.x < cx ? 0.08f : -0.05f));
            }
        }

        static void Leg(Frame f, int x0, int x1, int lift, int bob)
        {
            f.Rect(x0, 6 + lift, x1, 16 + bob, Pants, 0.5f);
            f.Fold(x0 + 2f, 12f + bob, x0 + 1.5f, 8f + lift, Pants, 0.3f); // складка у колена
            // Сапог: голенище с отворотом и носок, носок светлее.
            f.Rect(x0 - 1, lift, x1 + 1, 6 + lift, Boots, 0.4f);
            f.Band(x0 - 1, x1 + 1, 6.5f + lift, 0.6f, Boots, 0.72f);
            f.Disc(x0 + 0.8f, 1.2f + lift, 0.9f, Boots, 0.75f);
        }

        static void SideLeg(Frame f, int hipX, int footShift, int bob, float tone)
        {
            // Бедро от пояса к колену, голень от колена к стопе.
            f.Quad(hipX - 2f, 16.5f + bob, hipX + 2f, 16.5f + bob,
                hipX + footShift * 0.95f + 2f, 6f, hipX + footShift * 0.95f - 2f, 6f, Pants, tone);
            var footX = hipX + footShift;
            f.Rect(footX - 2, 0, footX + 4, 6, Boots, tone - 0.1f);
            f.Band(footX - 2, footX + 1, 6.5f, 0.6f, Boots, tone + 0.25f);
            f.Disc(footX + 3.5f, 1.8f, 0.9f, Boots, tone + 0.3f);
        }

        static void Arm(Frame f, int x0, int x1, int shoulderY, int swing, bool left)
        {
            // Рукав темнее туники; сторона к телу — в тени. Кисть сдвигается на шаге.
            f.Rect(x0, 23 + swing, x1, shoulderY, Tunic, 0.4f + (left ? 0.06f : 0f));
            var inner = left ? x1 + 0.75f : x0 + 0.25f;
            f.Fold(inner, shoulderY - 0.5f, inner, 23.5f + swing, Tunic, 0.12f);
            f.Band(x0, x1, 23f + swing, 0.5f, Tunic, 0.2f); // манжета
            f.Ellipse(x0 + (x1 - x0 + 1) * 0.5f, 21.2f + swing, 1.7f, 1.8f, Skin, 0.2f);
        }

        static void SideArm(Frame f, int shoulderX, int shoulderY, int swing, float tone)
        {
            f.Quad(shoulderX - 1.2f, shoulderY + 0.5f, shoulderX + 1.8f, shoulderY + 0.5f,
                shoulderX + swing + 1.5f, 23f, shoulderX + swing - 1.2f, 23f, Tunic, tone);
            f.Fold(shoulderX - 0.8f, shoulderY, shoulderX + swing - 0.8f, 23.5f, Tunic, tone - 0.15f);
            f.Ellipse(shoulderX + swing + 0.2f, 21.3f, 1.6f, 1.8f, Skin, tone);
        }

        static void Belt(Frame f, int x0, int x1, int y, bool buckle)
        {
            f.Rect(x0, y - 1, x1, y, Leather, 0.55f);
            f.Band(x0, x1, y + 0.6f, 0.4f, Leather, 0.85f); // светлая кромка ремня
            if (!buckle)
                return;
            f.FineRectRamp(Mid - 1.2f, y - 1f, Mid + 1.2f, y + 1f, Gold, 0.55f);
            f.FineRectRamp(Mid - 0.6f, y - 0.4f, Mid + 0.6f, y + 0.4f, Leather, 0.3f);
        }

        static void Mirror(PixelCanvas canvas)
        {
            for (var y = 0; y < canvas.Height; y++)
            for (var x = 0; x < canvas.Width / 2; x++)
            {
                var a = canvas.Get(x, y);
                var b = canvas.Get(canvas.Width - 1 - x, y);
                canvas.Set(x, y, b);
                canvas.Set(canvas.Width - 1 - x, y, a);
            }
        }

        /// <summary>
        /// Холст кадра: формы задаются в проектных пикселях, растеризуются с детализацией scale.
        /// Каждый пиксель помнит палитру своей части; в конце край силуэта затемняется в тень этой палитры.
        /// </summary>
        sealed class Frame
        {
            readonly int _w, _h, _s;
            readonly Color32[] _pixels;
            readonly ColorRamp[] _ramps;

            public Frame(int designWidth, int designHeight, int scale)
            {
                _s = scale;
                _w = designWidth * scale;
                _h = designHeight * scale;
                _pixels = new Color32[_w * _h];
                _ramps = new ColorRamp[_w * _h];
            }

            bool Inside(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;

            /// <summary>Пиксель высокой детализации (координаты — в пикселях холста).</summary>
            void Put(int x, int y, ColorRamp ramp, float t)
            {
                if (!Inside(x, y))
                    return;
                // Кожа — чистыми ступенями (дизеринг на лице читается как грязь), ткань и волосы — с лёгким.
                _pixels[y * _w + x] = ramp == Skin ? ramp.Sample(t) : ramp.SampleDithered(t, x, y, 0.15f);
                _ramps[y * _w + x] = ramp;
            }

            void PutColor(int x, int y, Color32 color)
            {
                if (!Inside(x, y))
                    return;
                _pixels[y * _w + x] = color;
                _ramps[y * _w + x] = null; // глаза и блики контуром не затемняются
            }

            /// <summary>Перебор пикселей холста внутри области (проектные координаты) с центрами пикселей в проектных единицах.</summary>
            void ForArea(float x0, float y0, float x1, float y1, System.Action<int, int, float, float> action)
            {
                var px0 = Mathf.FloorToInt(x0 * _s);
                var py0 = Mathf.FloorToInt(y0 * _s);
                var px1 = Mathf.CeilToInt(x1 * _s);
                var py1 = Mathf.CeilToInt(y1 * _s);
                for (var py = py0; py < py1; py++)
                for (var px = px0; px < px1; px++)
                    action(px, py, (px + 0.5f) / _s, (py + 0.5f) / _s);
            }

            /// <summary>Прямоугольник в проектных пикселях (включительно) с объёмом: светлее слева и сверху.</summary>
            public void Rect(int x0, int y0, int x1, int y1, ColorRamp ramp, float tone)
            {
                ForArea(x0, y0, x1 + 1, y1 + 1, (px, py, x, y) =>
                {
                    var u = (x - x0) / (x1 + 1f - x0);
                    var v = (y - y0) / (y1 + 1f - y0);
                    Put(px, py, ramp, tone + (0.5f - u) * 0.3f + (v - 0.5f) * 0.12f);
                });
            }

            /// <summary>Тонкая горизонтальная полоса толщиной thickness на высоте y.</summary>
            public void Band(int x0, int x1, float y, float thickness, ColorRamp ramp, float tone) =>
                ForArea(x0, y - thickness * 0.5f, x1 + 1, y + thickness * 0.5f, (px, py, x, _) =>
                    Put(px, py, ramp, tone + (0.5f - (x - x0) / (x1 + 1f - x0)) * 0.2f));

            /// <summary>Трапеция: верхняя грань [xt0, xt1+1] на yTop, нижняя [xb0, xb1+1] на yBottom.</summary>
            public void Trapezoid(int xt0, int xt1, float yTop, int xb0, int xb1, float yBottom, ColorRamp ramp, float tone) =>
                Quad(xt0, yTop, xt1 + 1, yTop, xb1 + 1, yBottom, xb0, yBottom, ramp, tone);

            /// <summary>Четырёхугольник (верх-лево, верх-право, низ-право, низ-лево) с объёмом цилиндра.</summary>
            public void Quad(float tlx, float tly, float trx, float try_, float brx, float bry, float blx, float bly,
                ColorRamp ramp, float tone)
            {
                var minX = Mathf.Min(Mathf.Min(tlx, trx), Mathf.Min(brx, blx));
                var maxX = Mathf.Max(Mathf.Max(tlx, trx), Mathf.Max(brx, blx));
                var minY = Mathf.Min(Mathf.Min(tly, try_), Mathf.Min(bry, bly));
                var maxY = Mathf.Max(Mathf.Max(tly, try_), Mathf.Max(bry, bly));
                ForArea(minX, minY, maxX, maxY, (px, py, x, y) =>
                {
                    var v = Mathf.InverseLerp(Mathf.Lerp(bly, tly, 0.5f), Mathf.Lerp(bry, try_, 0.5f), y);
                    var vl = Mathf.InverseLerp(bly, tly, y);
                    var vr = Mathf.InverseLerp(bry, try_, y);
                    if (y < Mathf.Min(bly, bry) || y > Mathf.Max(tly, try_))
                        return;
                    var left = Mathf.Lerp(blx, tlx, vl);
                    var right = Mathf.Lerp(brx, trx, vr);
                    if (x < left || x > right)
                        return;
                    var u = (x - left) / Mathf.Max(0.01f, right - left);
                    // Цилиндр: свет слева, тень справа, края темнее.
                    var round = 1f - Mathf.Pow(Mathf.Abs(u - 0.4f) * 1.6f, 2f);
                    Put(px, py, ramp, tone + (0.5f - u) * 0.3f + round * 0.12f + (v - 0.5f) * 0.1f);
                });
            }

            public void Triangle(float ax, float ay, float bx, float by, float cx, float cy, ColorRamp ramp, float tone)
            {
                ForArea(Mathf.Min(ax, Mathf.Min(bx, cx)), Mathf.Min(ay, Mathf.Min(by, cy)),
                    Mathf.Max(ax, Mathf.Max(bx, cx)), Mathf.Max(ay, Mathf.Max(by, cy)), (px, py, x, y) =>
                    {
                        var d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
                        var d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
                        var d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
                        var neg = d1 < 0 || d2 < 0 || d3 < 0;
                        var pos = d1 > 0 || d2 > 0 || d3 > 0;
                        if (!(neg && pos))
                            Put(px, py, ramp, tone + (y - Mathf.Min(ay, Mathf.Min(by, cy))) * 0.04f);
                    });
            }

            /// <summary>Эллипсоид с освещением сверху-слева.</summary>
            public void Ellipse(float cx, float cy, float rx, float ry, ColorRamp ramp, float tone) =>
                EllipseWhere(cx, cy, rx, ry, ramp, (_, _) => true, tone);

            public void EllipseWhere(float cx, float cy, float rx, float ry, ColorRamp ramp,
                System.Func<float, float, bool> mask, float tone = 0.15f)
            {
                ForArea(cx - rx, cy - ry, cx + rx, cy + ry, (px, py, x, y) =>
                {
                    var nx = (x - cx) / rx;
                    var ny = (y - cy) / ry;
                    var d = nx * nx + ny * ny;
                    if (d > 1f || !mask(x, y))
                        return;
                    var normal = new Vector3(nx, ny, Mathf.Sqrt(1f - d));
                    Put(px, py, ramp, tone + Mathf.Clamp01(Vector3.Dot(normal, Light)) * 0.8f);
                });
            }

            public void Disc(float cx, float cy, float r, ColorRamp ramp, float tone) =>
                ForArea(cx - r, cy - r, cx + r, cy + r, (px, py, x, y) =>
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                        Put(px, py, ramp, tone - (x - cx) * 0.08f + (y - cy) * 0.08f);
                });

            /// <summary>Линия толщиной в один пиксель холста.</summary>
            public void Stroke(float x0, float y0, float x1, float y1, ColorRamp ramp, float tone, bool onlyOver = false)
            {
                var length = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * _s;
                for (var i = 0; i <= length; i++)
                {
                    var t = length < 1f ? 0f : i / length;
                    var px = Mathf.FloorToInt(Mathf.Lerp(x0, x1, t) * _s);
                    var py = Mathf.FloorToInt(Mathf.Lerp(y0, y1, t) * _s);
                    if (onlyOver && (!Inside(px, py) || _ramps[py * _w + px] != ramp))
                        continue;
                    Put(px, py, ramp, tone);
                }
            }

            /// <summary>Линия только поверх уже нарисованной этой же палитрой части (пряди не вылезают за волосы).</summary>
            public void StrokeInside(float x0, float y0, float x1, float y1, ColorRamp ramp, float tone) =>
                Stroke(x0, y0, x1, y1, ramp, tone, true);

            /// <summary>Складка: тонкая тень поверх части.</summary>
            public void Fold(float x0, float y0, float x1, float y1, ColorRamp ramp, float tone) =>
                Stroke(x0, y0, x1, y1, ramp, tone, true);

            /// <summary>Один пиксель высокой детализации в проектных координатах.</summary>
            public void Fine(float x, float y, ColorRamp ramp, float tone) =>
                Put(Mathf.FloorToInt(x * _s), Mathf.FloorToInt(y * _s), ramp, tone);

            public void FineRect(float x0, float y0, float x1, float y1, Color32 color) =>
                ForArea(x0, y0, x1, y1, (px, py, _, _) => PutColor(px, py, color));

            public void FineRectRamp(float x0, float y0, float x1, float y1, ColorRamp ramp, float tone) =>
                ForArea(x0, y0, x1, y1, (px, py, x, y) => Put(px, py, ramp, tone + (y - y0) / (y1 - y0) * 0.4f - (x - x0) / (x1 - x0) * 0.2f));

            /// <summary>Неровный подол: вырезы снизу через равные промежутки.</summary>
            public void Hem(int x0, int x1, float y, int period)
            {
                for (var x = x0; x <= x1; x += period)
                    ForArea(x, y - 0.6f, x + 1, y + 0.6f, (px, py, _, _) =>
                    {
                        if (!Inside(px, py))
                            return;
                        _pixels[py * _w + px] = default;
                        _ramps[py * _w + px] = null;
                    });
            }

            /// <summary>Знак на одежде (совпадает со знаком в святилище): маленький светящийся ромб.</summary>
            public void Glyph(float cx, float cy) =>
                ForArea(cx - 1.4f, cy - 1.4f, cx + 1.4f, cy + 1.4f, (px, py, x, y) =>
                {
                    var d = Mathf.Abs(x - cx) + Mathf.Abs(y - cy);
                    if (d <= 1.3f)
                        Put(px, py, GlyphGlow, 1f - d * 0.45f);
                });

            public PixelCanvas Resolve()
            {
                var canvas = new PixelCanvas(_w, _h);
                for (var y = 0; y < _h; y++)
                for (var x = 0; x < _w; x++)
                {
                    var i = y * _w + x;
                    if (_pixels[i].a == 0)
                        continue;
                    var color = _pixels[i];
                    // Край силуэта — тень своей части (цветной мягкий контур толщиной в пиксель холста).
                    if (_ramps[i] != null && IsEdge(x, y))
                        color = _ramps[i][y < _h / 2 ? 0 : 1];
                    canvas.Set(x, y, color);
                }
                return canvas;
            }

            bool IsEdge(int x, int y) =>
                Empty(x - 1, y) || Empty(x + 1, y) || Empty(x, y - 1) || Empty(x, y + 1);

            bool Empty(int x, int y) => !Inside(x, y) || _pixels[y * _w + x].a == 0;
        }
    }
}
