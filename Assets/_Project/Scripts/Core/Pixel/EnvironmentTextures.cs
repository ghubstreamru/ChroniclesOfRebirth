using System.Collections.Generic;
using UnityEngine;

namespace ChroniclesOfRebirth.Core.Pixel
{
    /// <summary>
    /// Генераторы бесшовных пиксельных текстур окружения. Плитка покрывает <see cref="Meshes.MeshBuilder.UvUnitsPerTile"/>
    /// юнита, поэтому плотность пикселей задаётся размером плитки: <see cref="TileSize"/> px = <see cref="TexelsPerUnit"/> px/юнит.
    /// Размеры деталей заданы для эталонной плитки 64 px и умножаются на масштаб k = size / 64: в мире детали
    /// остаются того же размера, но рисуются вдвое большим числом пикселей — мягче и подробнее.
    /// Цвета берутся из <see cref="AutumnPalette"/>.
    /// </summary>
    public static class EnvironmentTextures
    {
        /// <summary>Плотность пикселей текстур окружения (рельеф, кора, камень); у спрайтов та же — 32 px/юнит.</summary>
        public const int TexelsPerUnit = 32;

        /// <summary>Размер плитки текстуры окружения: 4 юнита × <see cref="TexelsPerUnit"/>.</summary>
        public const int TileSize = 128;

        static int Scale(int size) => Mathf.Max(1, size / 64);

        public static Texture2D Grass(int size, int seed) => Meadow(size, seed, false, "Grass");

        /// <summary>Трава с опавшей листвой — для земли под осенними деревьями.</summary>
        public static Texture2D GrassWithLeaves(int size, int seed) => Meadow(size, seed, true, "GrassWithLeaves");

        /// <summary>
        /// Луговой покров: густые травинки (зелёные и сухие вперемешку), местами сквозь них видна тёмная земля,
        /// мелкие веточки и камешки. Без «кочек»-пузырей — неровный природный покров.
        /// </summary>
        static Texture2D Meadow(int size, int seed, bool leaves, string name)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var grass = AutumnPalette.Grass;
            var soil = AutumnPalette.Dirt;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var n = PixelNoise.Fbm(x, y, size, 16 * k, 3 + k / 2, seed);
                var patch = PixelNoise.Fbm(x, y, size, 32 * k, 3, seed + 1);
                canvas.Set(x, y, patch < 0.33f
                    ? soil.SampleDithered(0.2f + (n - 0.5f) * 0.3f, x, y)            // проплешина: тёмная земля
                    : grass.SampleDithered(0.28f + (n - 0.5f) * 0.36f - (patch < 0.4f ? 0.08f : 0f), x, y));
            }

            DrawBlades(canvas, grass, AutumnPalette.DryGrass, size * size / (3 * k), seed + 2, leaves ? 0.3f : 0.2f, k);
            DrawDebris(canvas, size * size / (200 * k * k), seed + 4, k);
            if (leaves)
                ScatterLeaves(canvas, size * size / (16 * k * k), seed + 9, k);
            return canvas.ToTexture(name, true);
        }

        public static Texture2D ForestFloor(int size, int seed)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var ramp = AutumnPalette.ForestFloor;
            var moss = AutumnPalette.Moss;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var n = PixelNoise.Fbm(x, y, size, 16 * k, 3 + k / 2, seed);
                var m = PixelNoise.Fbm(x, y, size, 32 * k, 3, seed + 2);
                canvas.Set(x, y, m > 0.6f
                    ? moss.SampleDithered(0.25f + (m - 0.6f) * 1.6f + (n - 0.5f) * 0.3f, x, y) // подушки мха
                    : ramp.SampleDithered(0.4f + (n - 0.5f) * 0.5f, x, y));
            }

            DrawBlades(canvas, AutumnPalette.Grass, AutumnPalette.DryGrass, size * size / (14 * k), seed + 3, 0.3f, k);
            DrawNeedles(canvas, size * size / (14 * k), seed + 7, k);
            DrawDebris(canvas, size * size / (150 * k * k), seed + 6, k);
            ScatterLeaves(canvas, size * size / (10 * k * k), seed + 5, k);
            return canvas.ToTexture("ForestFloor", true);
        }

        /// <summary>Мелкий мусор: веточки и камешки с тенью.</summary>
        static void DrawDebris(PixelCanvas canvas, int count, int seed, int k)
        {
            var size = canvas.Width;
            var bark = AutumnPalette.Bark;
            var stone = AutumnPalette.Pebble;
            for (var i = 0; i < count; i++)
            {
                var x = Mathf.FloorToInt(PixelNoise.Hash01(i, 0, seed) * size);
                var y = Mathf.FloorToInt(PixelNoise.Hash01(i, 1, seed) * size);
                if (PixelNoise.Hash01(i, 2, seed) < 0.55f)
                {
                    // Веточка: тонкий отрезок, светлый сверху, тень под ним.
                    var angle = PixelNoise.Hash01(i, 3, seed) * Mathf.PI;
                    var length = (2f + PixelNoise.Hash01(i, 4, seed) * 3f) * k;
                    for (var j = 0f; j < length; j += 1f)
                    {
                        var px = Mathf.RoundToInt(x + Mathf.Cos(angle) * j);
                        var py = Mathf.RoundToInt(y + Mathf.Sin(angle) * j);
                        canvas.SetWrapped(px, py - 1, bark[0]);
                        canvas.SetWrapped(px, py, bark.Sample(0.45f + j / length * 0.25f));
                    }
                }
                else
                {
                    // Камешек 1–2 px с тенью снизу.
                    var r = k > 1 && PixelNoise.Hash01(i, 5, seed) > 0.5f ? 1 : 0;
                    for (var dy = 0; dy <= r; dy++)
                    for (var dx = 0; dx <= r; dx++)
                        canvas.SetWrapped(x + dx, y + dy, stone.Sample(0.55f + dy * 0.15f - dx * 0.05f));
                    canvas.SetWrapped(x, y - 1, stone[0]);
                }
            }
        }

        public static Texture2D Dirt(int size, int seed)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var ramp = AutumnPalette.Dirt;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var n = PixelNoise.Fbm(x, y, size, 16 * k, 3 + k / 2, seed);
                // Утоптанная колея светлее, края тропы темнее — мягкие пятна вместо ровной заливки.
                var worn = PixelNoise.Fbm(x, y, size, 32 * k, 2, seed + 3);
                canvas.Set(x, y, ramp.SampleDithered(0.45f + (n - 0.5f) * 0.5f + (worn - 0.5f) * 0.18f, x, y));
            }

            // Мелкие камешки: светлая макушка, тень снизу.
            var specks = size * size / (70 * k * k);
            for (var i = 0; i < specks; i++)
            {
                var x = Mathf.FloorToInt(PixelNoise.Hash01(i, 0, seed + 7) * size);
                var y = Mathf.FloorToInt(PixelNoise.Hash01(i, 1, seed + 7) * size);
                var r = k > 1 && PixelNoise.Hash01(i, 2, seed + 7) > 0.6f ? 1 : 0;
                for (var dy = -r; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                    canvas.SetWrapped(x + dx, y + dy, ramp[ramp.Count - 2 - (dy < 0 ? 2 : 0)]);
                canvas.SetWrapped(x, y - r - 1, ramp[1]);
            }

            return canvas.ToTexture("Dirt", true);
        }

        public static Texture2D Pebbles(int size, int seed) =>
            PebblePattern(size, 8, AutumnPalette.Pebble, AutumnPalette.PebbleGap, seed, "Pebbles");

        /// <summary>Речной берег: песок с мелким гравием и редкими крупными камешками.</summary>
        public static Texture2D Gravel(int size, int seed)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var sand = AutumnPalette.Sand;
            var stone = AutumnPalette.Pebble;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var n = PixelNoise.Fbm(x, y, size, 16 * k, 3 + k / 2, seed);
                var color = sand.SampleDithered(0.38f + (n - 0.5f) * 0.55f, x, y);

                // Мелкие камешки (ячейка 4 px эталона), заметная часть ячеек — песок.
                PixelNoise.Voronoi(x, y, size, 4 * k, 0.8f, seed + 1, out var d1, out var d2, out var id, out var offset);
                if (PixelNoise.Hash01(id, 0, seed) > 0.42f && d2 - d1 > 0.8f * k && d1 < 2.2f * k)
                {
                    var tone = PixelNoise.Hash01(id, 1, seed);
                    color = stone.SampleDithered(0.2f + tone * 0.45f + (-offset.x + offset.y) / k * 0.12f, x, y, 0.5f);
                }

                // Редкие крупные камни (ячейка 16 px эталона).
                PixelNoise.Voronoi(x, y, size, 16 * k, 0.7f, seed + 2, out var b1, out _, out var bigId, out var bigOffset);
                if (PixelNoise.Hash01(bigId, 2, seed) > 0.7f && b1 < 3.5f * k)
                    color = stone.SampleDithered(0.38f + (-bigOffset.x + bigOffset.y) / k * 0.07f, x, y, 0.5f);
                else if (PixelNoise.Hash01(bigId, 2, seed) > 0.7f && b1 < 4.3f * k)
                    color = sand[0];

                canvas.Set(x, y, color);
            }

            return canvas.ToTexture("Gravel", true);
        }

        public static Texture2D Riverbed(int size, int seed) =>
            PebblePattern(size, 8, AutumnPalette.Riverbed, AutumnPalette.RiverbedGap, seed, "Riverbed");

        static Texture2D PebblePattern(int size, int baseCell, ColorRamp stone, ColorRamp gap, int seed, string name)
        {
            var k = Scale(size);
            var cell = baseCell * k;
            var canvas = new PixelCanvas(size, size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                PixelNoise.Voronoi(x, y, size, cell, 0.85f, seed, out var d1, out var d2, out var id, out var offset);
                var edge = d2 - d1;
                if (edge < 0.8f * k)
                {
                    canvas.Set(x, y, gap.Sample(PixelNoise.Hash01(x, y, seed + 1) * 0.99f));
                    continue;
                }

                // Округлый камень: свет сверху-слева, мягкий спад к краю.
                var tone = PixelNoise.Hash01(id, 5, seed);
                var light = (-offset.x + offset.y) / cell;
                var rim = Mathf.Clamp01((edge - 0.8f * k) / (2.6f * k));
                var t = 0.22f + tone * 0.38f + light * 0.3f + rim * 0.12f;
                canvas.Set(x, y, stone.SampleDithered(t, x, y, 0.6f));
            }

            return canvas.ToTexture(name, true);
        }

        /// <summary>Каменные плиты пола святилища, с мхом.</summary>
        public static Texture2D Flagstones(int size, int seed) =>
            BlockPattern(size, 16, 12, 20, 0.6f, seed, "Flagstones");

        /// <summary>Каменная кладка для стен, колонн и боковин плит.</summary>
        public static Texture2D StoneWall(int size, int seed) =>
            BlockPattern(size, 8, 10, 16, 0.68f, seed, "StoneWall");

        static Texture2D BlockPattern(int size, int baseRowHeight, int baseMinWidth, int baseMaxWidth, float mossThreshold,
            int seed, string name)
        {
            var k = Scale(size);
            var rowHeight = baseRowHeight * k;
            var minWidth = baseMinWidth * k;
            var maxWidth = baseMaxWidth * k;
            var canvas = new PixelCanvas(size, size);
            var stone = AutumnPalette.Stone;
            var moss = AutumnPalette.Moss;
            var rows = size / rowHeight;

            for (var row = 0; row < rows; row++)
            {
                // Ширины блоков в ряду, сумма ровно size — чтобы ряд тайлился.
                var widths = new List<int>();
                var total = 0;
                var n = 0;
                while (total < size)
                {
                    var steps = (maxWidth - minWidth) / (2 * k) + 1;
                    var w = minWidth + Mathf.FloorToInt(PixelNoise.Hash01(row, n++, seed + 1) * steps) * 2 * k;
                    if (size - total - w < minWidth)
                        w = size - total;
                    widths.Add(w);
                    total += w;
                }

                var shift = Mathf.FloorToInt(PixelNoise.Hash01(row, 0, seed) * size);
                var y0 = row * rowHeight;

                for (var ly = 0; ly < rowHeight; ly++)
                for (var x = 0; x < size; x++)
                {
                    var y = y0 + ly;
                    var rx = ((x - shift) % size + size) % size;
                    var block = 0;
                    var bx = 0;
                    while (block < widths.Count - 1 && rx >= bx + widths[block])
                    {
                        bx += widths[block];
                        block++;
                    }

                    var lx = rx - bx;
                    var bw = widths[block];
                    var m = PixelNoise.Fbm(x, y, size, 16 * k, 3, seed + 11);
                    var isGap = lx == 0 || ly == 0;

                    if (isGap)
                    {
                        canvas.Set(x, y, m > mossThreshold - 0.14f ? moss[1] : AutumnPalette.Mortar[ly == 0 ? 0 : 1]);
                        continue;
                    }

                    // Скруглённые края блока: фаска светлее сверху-слева, темнее снизу-справа.
                    var tone = PixelNoise.Hash01(row * 31 + block, 9, seed);
                    var t = 0.3f + tone * 0.32f + (PixelNoise.Fbm(x, y, size, 8 * k, 2 + k / 2, seed + 3) - 0.5f) * 0.25f;
                    if (ly >= rowHeight - k || lx <= k)
                        t += 0.13f;
                    if (ly <= k || lx >= bw - k)
                        t -= 0.11f;

                    canvas.Set(x, y, m > mossThreshold
                        ? moss.SampleDithered(0.25f + (m - mossThreshold) * 2.5f + t * 0.3f, x, y)
                        : stone.SampleDithered(t, x, y, 0.7f));
                }
            }

            return canvas.ToTexture(name, true);
        }

        /// <summary>Земляной обрыв со слоями и вкраплениями камней — боковины рельефа.</summary>
        public static Texture2D CliffEarth(int size, int seed)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var earth = AutumnPalette.Earth;
            var rock = AutumnPalette.Rock;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var strata = PixelNoise.ValueAniso(x / (16f * k), y / (4f * k), size / (16 * k), size / (4 * k), seed);
                var t = 0.3f + strata * 0.35f + (PixelNoise.Fbm(x, y, size, 8 * k, 2 + k / 2, seed + 1) - 0.5f) * 0.3f;
                var color = earth.SampleDithered(t, x, y);

                // Корешки и камни в земле: округлые, со светлой макушкой.
                PixelNoise.Voronoi(x, y, size, 16 * k, 0.9f, seed + 5, out var d1, out _, out var id, out var offset);
                if (d1 < 3f * k && PixelNoise.Hash01(id, 1, seed) > 0.45f)
                    color = rock.SampleDithered(0.38f + (-offset.x + offset.y) / k * 0.08f, x, y);

                canvas.Set(x, y, color);
            }

            return canvas.ToTexture("CliffEarth", true);
        }

        /// <summary>Горная скала для боковин: горизонтальные пласты, вертикальные трещины, редкие пятна лишайника.</summary>
        public static Texture2D CliffRock(int size, int seed)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var rock = AutumnPalette.CliffRock;
            var moss = AutumnPalette.Moss;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Пласты: полосы разной толщины с неровной границей.
                var strata = PixelNoise.ValueAniso(x / (32f * k), y / (8f * k), size / (32 * k), size / (8 * k), seed);
                var t = 0.3f + strata * 0.4f + (PixelNoise.Fbm(x, y, size, 8 * k, 2 + k / 2, seed + 1) - 0.5f) * 0.25f;

                // Блоки, расколотые трещинами: тёмный шов, светлый верхний край под ним.
                PixelNoise.Voronoi(x, y, size, 16 * k, 0.8f, seed + 3, out var d1, out var d2, out _, out var offset);
                if (d2 - d1 < 0.9f * k)
                    t = 0.05f;
                else
                    t += (offset.y * 0.03f - offset.x * 0.015f) / k;

                var lichen = PixelNoise.Fbm(x, y, size, 16 * k, 2, seed + 7);
                canvas.Set(x, y, lichen > 0.72f && d2 - d1 >= 0.9f * k
                    ? moss.SampleDithered(0.2f + (lichen - 0.72f) * 3f, x, y)
                    : rock.SampleDithered(t, x, y, 0.6f));
            }

            return canvas.ToTexture("CliffRock", true);
        }

        /// <summary>Снег: мягкие сугробы, рябь от ветра и редкие искры.</summary>
        public static Texture2D Snow(int size, int seed)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var snow = AutumnPalette.Snow;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var drift = PixelNoise.Fbm(x, y, size, 16 * k, 3 + k / 2, seed);
                var ripple = PixelNoise.ValueAniso(x / (16f * k), y / (4f * k), size / (16 * k), size / (4 * k), seed + 1);
                var t = 0.6f + (drift - 0.5f) * 0.4f + (ripple - 0.5f) * 0.14f;
                if (PixelNoise.Hash01(x, y, seed + 2) > 1f - 0.015f / k)
                    t = 1f;
                canvas.Set(x, y, snow.SampleDithered(t, x, y, 0.6f));
            }

            return canvas.ToTexture("Snow", true);
        }

        /// <summary>Доски: горизонтальные ряды с волокнами, тёмными щелями и гвоздями.</summary>
        public static Texture2D Planks(int size, int seed)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var wood = AutumnPalette.Wood;
            var board = 8 * k;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var row = y / board;
                var ly = y % board;
                if (ly == 0)
                {
                    canvas.Set(x, y, wood[0]);
                    continue;
                }

                // Стык досок в ряду — в случайном месте.
                var joint = Mathf.FloorToInt(PixelNoise.Hash01(row, 0, seed) * size);
                if (x == joint)
                {
                    canvas.Set(x, y, wood[0]);
                    continue;
                }

                var tone = PixelNoise.Hash01(row, x > joint ? 1 : 2, seed) * 0.25f;
                var grain = PixelNoise.ValueAniso(x / (8f * k), y / (2f * k), size / (8 * k), size / (2 * k), seed + row);
                var t = 0.3f + tone + (grain - 0.5f) * 0.35f;
                if (ly >= board - k)
                    t += 0.12f;
                if (ly <= k)
                    t -= 0.1f;
                var color = wood.SampleDithered(t, x, y, 0.5f);
                if ((x == (joint + 2 * k) % size || x == (joint + size - 3 * k) % size) && ly == board / 2)
                    color = ColorRamp.Hex("2a2a2a");
                canvas.Set(x, y, color);
            }

            return canvas.ToTexture("Planks", true);
        }

        /// <summary>Грубая ткань палатки: переплетение и заплатки.</summary>
        public static Texture2D Canvas(int size, int seed)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var ramp = AutumnPalette.Canvas;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var weave = ((x + y) & 1) == 0 ? 0.04f : -0.04f;
                var n = PixelNoise.Fbm(x, y, size, 16 * k, 3, seed);
                var t = 0.45f + (n - 0.5f) * 0.5f + weave;
                PixelNoise.Voronoi(x, y, size, 32 * k, 0.6f, seed + 1, out var d1, out _, out var id, out _);
                if (PixelNoise.Hash01(id, 0, seed) > 0.75f && d1 < 7f * k)
                    t -= 0.18f;
                canvas.Set(x, y, ramp.SampleDithered(t, x, y, 0.6f));
            }

            return canvas.ToTexture("Canvas", true);
        }

        public static Texture2D Rock(int size, int seed)
        {
            var k = Scale(size);
            var canvas = new PixelCanvas(size, size);
            var rock = AutumnPalette.Rock;
            var moss = AutumnPalette.Moss;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Камень: зернистость и слоистость; трещины редкие и тонкие (не «сетка мяча»).
                var t = 0.42f + (PixelNoise.Fbm(x, y, size, 16 * k, 4 + k / 2, seed) - 0.5f) * 0.65f;
                t += (PixelNoise.ValueAniso(x / (32f * k), y / (8f * k), size / (32 * k), size / (8 * k), seed + 5) - 0.5f) * 0.18f;
                PixelNoise.Voronoi(x, y, size, 32 * k, 0.9f, seed + 3, out var d1, out var d2, out var crackId, out _);
                if (d2 - d1 < 0.7f * k && PixelNoise.Hash01(crackId, 0, seed) > 0.5f)
                    t -= 0.14f;
                if (PixelNoise.Hash01(x, y, seed + 4) > 1f - 0.03f / k)
                    t += 0.15f;

                var m = PixelNoise.Fbm(x, y, size, 16 * k, 2, seed + 8);
                canvas.Set(x, y, m > 0.68f
                    ? moss.SampleDithered(0.3f + (m - 0.68f) * 3f, x, y)
                    : rock.SampleDithered(t, x, y, 0.7f));
            }

            return canvas.ToTexture("Rock", true);
        }

        /// <summary>Травинки: тонкие штрихи от тёмного основания к светлому кончику, слегка наклонённые.</summary>
        static void DrawBlades(PixelCanvas canvas, ColorRamp ramp, ColorRamp dry, int count, int seed, float dryChance, int k)
        {
            var size = canvas.Width;
            for (var i = 0; i < count; i++)
            {
                var x = PixelNoise.Hash01(i, 0, seed) * size;
                var y = PixelNoise.Hash01(i, 1, seed) * size;
                var length = (1.5f + PixelNoise.Hash01(i, 2, seed) * 2f) * k;
                var lean = (PixelNoise.Hash01(i, 3, seed) - 0.5f) * 0.7f;
                var rp = PixelNoise.Hash01(i, 4, seed) < dryChance ? dry : ramp;
                var tone = 0.25f + PixelNoise.Hash01(i, 5, seed) * 0.25f;
                for (var j = 0f; j < length; j += 1f)
                {
                    var f = j / length;
                    canvas.SetWrapped(Mathf.RoundToInt(x + lean * j), Mathf.RoundToInt(y + j), rp.Sample(tone + f * 0.35f));
                }
            }
        }

        /// <summary>Опавшая хвоя: короткие тёмные штрихи в разные стороны.</summary>
        static void DrawNeedles(PixelCanvas canvas, int count, int seed, int k)
        {
            var size = canvas.Width;
            var ramp = AutumnPalette.Bark;
            for (var i = 0; i < count; i++)
            {
                var x = PixelNoise.Hash01(i, 0, seed) * size;
                var y = PixelNoise.Hash01(i, 1, seed) * size;
                var angle = PixelNoise.Hash01(i, 2, seed) * Mathf.PI;
                var length = (1.5f + PixelNoise.Hash01(i, 3, seed) * 1.5f) * k;
                var color = ramp.Sample(0.35f + PixelNoise.Hash01(i, 4, seed) * 0.4f);
                for (var j = 0f; j < length; j += 1f)
                    canvas.SetWrapped(Mathf.RoundToInt(x + Mathf.Cos(angle) * j), Mathf.RoundToInt(y + Mathf.Sin(angle) * j), color);
            }
        }

        /// <summary>Опавшие листья: маленькие ромбики с тенью снизу и бликом сверху.</summary>
        static void ScatterLeaves(PixelCanvas canvas, int count, int seed, int k)
        {
            var ramps = new[] { AutumnPalette.LeavesOrange, AutumnPalette.LeavesRed, AutumnPalette.LeavesYellow };
            var size = canvas.Width;
            for (var i = 0; i < count; i++)
            {
                var x = Mathf.FloorToInt(PixelNoise.Hash01(i, 0, seed) * size);
                var y = Mathf.FloorToInt(PixelNoise.Hash01(i, 1, seed) * size);
                var ramp = ramps[Mathf.FloorToInt(PixelNoise.Hash01(i, 2, seed) * ramps.Length) % ramps.Length];
                var tone = 0.3f + PixelNoise.Hash01(i, 3, seed) * 0.5f;
                var r = k; // полуширина листа
                for (var dy = -r; dy <= r; dy++)
                for (var dx = -r - 1; dx <= r + 1; dx++)
                {
                    if (Mathf.Abs(dx) / (r + 1.5f) + Mathf.Abs(dy) / (r + 0.5f) > 1f)
                        continue;
                    canvas.SetWrapped(x + dx, y + dy, ramp.Sample(tone + dy * 0.08f - dx * 0.03f));
                }
                canvas.SetWrapped(x, y - r - 1, ramp[0]);
            }
        }
    }
}
