using System.IO;
using ChroniclesOfRebirth.Core.World;
using UnityEditor;
using UnityEngine;

namespace ChroniclesOfRebirth.Editor.Locations
{
    /// <summary>
    /// Карта локации сверху для обсуждения планировки: рельеф, вода, тропы, граница проходимой зоны,
    /// кроны деревьев, пронумерованные поляны. 1 пиксель = 1 клетка рельефа (0,25 юнита), север сверху.
    /// </summary>
    public static class SilverrunValeMap
    {
        const string OutputPath = "Docs/Design/Locations/Maps/SilverrunVale_map.png";

        static readonly Color32 PlayBorder = new(220, 60, 60, 255);
        static readonly Color32 WalkEdge = new(255, 250, 220, 255);
        static readonly Color32 Spawn = new(230, 60, 220, 255);
        static readonly Color32 GladeRing = new(255, 255, 255, 255);

        [MenuItem("Chronicles of Rebirth/Локации/Карта «Долина Серебрянки» (PNG)")]
        public static void Export()
        {
            var layout = new SilverrunValeLayout();
            var field = layout.Field;
            var w = field.Columns;
            var h = field.Rows;
            var pixels = new Color32[w * h];

            for (var r = 0; r < h; r++)
            for (var c = 0; c < w; c++)
            {
                var color = (Color)GroundColor(field.GetGround(c, r));
                var height = field.GetHeight(c, r);
                color *= Mathf.Clamp(0.78f + height * 0.07f, 0.55f, 1.15f);

                // Обрывы — тёмная линия, чтобы читались уровни.
                if (HeightStep(field, c, r) >= TerrainField.LevelHeight * 1.5f)
                    color *= 0.55f;
                color.a = 1f;
                pixels[r * w + c] = color;
            }

            // Кроны деревьев — полупрозрачные круги, показывают плотность леса.
            foreach (var tree in layout.Trees)
            {
                var crown = tree.Kind == SilverrunValeLayout.TreeKind.Conifer
                    ? new Color(0.10f, 0.22f, 0.13f)
                    : new Color(0.55f, 0.25f, 0.10f);
                var radius = (tree.Giant ? 8.5f : 1.9f) * tree.Scale;
                Disc(pixels, field, tree.Position, radius, crown, 0.45f);
            }

            // Граница проходимой зоны.
            for (var r = 0; r < h; r++)
            for (var c = 0; c < w; c++)
            {
                if (!layout.IsWalkable(c, r))
                    continue;
                if (!layout.IsWalkable(c + 1, r) || !layout.IsWalkable(c - 1, r) ||
                    !layout.IsWalkable(c, r + 1) || !layout.IsWalkable(c, r - 1))
                    pixels[r * w + c] = WalkEdge;
            }

            // Граница игровой зоны.
            Rect(pixels, field, 0f, 0f, SilverrunValeLayout.PlayWidth, SilverrunValeLayout.PlayDepth, PlayBorder);

            // Поляны: кольцо и номер.
            var number = 1;
            foreach (var glade in SilverrunValeLayout.GladeInfos())
            {
                Ring(pixels, field, glade.center, glade.radius, GladeRing);
                Digit(pixels, field, glade.center, number++);
            }

            Disc(pixels, field, new Vector2(SilverrunValeLayout.HeroSpawn.x, SilverrunValeLayout.HeroSpawn.z), 0.9f, Spawn, 1f);

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
            File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            Debug.Log($"[Chronicles] Карта сохранена: {OutputPath} ({w}×{h}, 1 px = {TerrainField.CellSize} юнита)");
        }

        static Color32 GroundColor(GroundType ground) => ground switch
        {
            GroundType.DeepBed => new Color32(43, 94, 107, 255),
            GroundType.ShallowBed => new Color32(95, 165, 163, 255),
            GroundType.Pebbles => new Color32(205, 185, 143, 255),
            GroundType.Grass => new Color32(143, 179, 90, 255),
            GroundType.GrassLeaves => new Color32(168, 160, 74, 255),
            GroundType.ForestFloor => new Color32(80, 106, 58, 255),
            GroundType.Dirt => new Color32(176, 128, 78, 255),
            GroundType.Flagstone => new Color32(160, 160, 150, 255),
            GroundType.Rock => new Color32(120, 112, 100, 255),
            GroundType.Snow => new Color32(240, 232, 225, 255),
            _ => new Color32(255, 0, 255, 255),
        };

        static float HeightStep(TerrainField field, int c, int r)
        {
            var h = field.GetHeight(c, r);
            var step = 0f;
            if (field.InBounds(c + 1, r)) step = Mathf.Max(step, Mathf.Abs(field.GetHeight(c + 1, r) - h));
            if (field.InBounds(c, r + 1)) step = Mathf.Max(step, Mathf.Abs(field.GetHeight(c, r + 1) - h));
            return step;
        }

        static void Blend(Color32[] pixels, TerrainField field, int c, int r, Color color, float alpha)
        {
            if (!field.InBounds(c, r))
                return;
            var i = r * field.Columns + c;
            var mixed = Color.Lerp(pixels[i], color, alpha);
            mixed.a = 1f;
            pixels[i] = mixed;
        }

        static void Disc(Color32[] pixels, TerrainField field, Vector2 center, float radius, Color color, float alpha)
        {
            field.TryCell(center.x, center.y, out var cc, out var cr);
            var rc = Mathf.CeilToInt(radius / TerrainField.CellSize);
            for (var dr = -rc; dr <= rc; dr++)
            for (var dc = -rc; dc <= rc; dc++)
            {
                if ((dc * dc + dr * dr) * TerrainField.CellSize * TerrainField.CellSize > radius * radius)
                    continue;
                Blend(pixels, field, cc + dc, cr + dr, color, alpha);
            }
        }

        static void Ring(Color32[] pixels, TerrainField field, Vector2 center, float radius, Color color)
        {
            for (var a = 0f; a < Mathf.PI * 2f; a += 0.01f)
            {
                var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                if (field.TryCell(p.x, p.y, out var c, out var r))
                    Blend(pixels, field, c, r, color, 1f);
            }
        }

        static void Rect(Color32[] pixels, TerrainField field, float x0, float z0, float x1, float z1, Color color)
        {
            for (var x = x0; x <= x1; x += TerrainField.CellSize)
            {
                Mark(x, z0);
                Mark(x, z1);
            }
            for (var z = z0; z <= z1; z += TerrainField.CellSize)
            {
                Mark(x0, z);
                Mark(x1, z);
            }

            void Mark(float x, float z)
            {
                if (field.TryCell(x, z, out var c, out var r))
                    Blend(pixels, field, c, r, color, 1f);
            }
        }

        // Пиксельные цифры 3×5 для номеров полян.
        static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
        };

        static void Digit(Color32[] pixels, TerrainField field, Vector2 center, int value)
        {
            field.TryCell(center.x, center.y, out var cc, out var cr);
            const int scale = 3;
            var pattern = Digits[value % 10];
            for (var y = 0; y < 5; y++)
            for (var x = 0; x < 3; x++)
            {
                var on = pattern[y * 3 + x] == '1';
                for (var sy = 0; sy < scale; sy++)
                for (var sx = 0; sx < scale; sx++)
                {
                    var c = cc + (x - 1) * scale + sx;
                    var r = cr + (2 - y) * scale + sy;
                    Blend(pixels, field, c, r, on ? Color.white : Color.black, on ? 1f : 0.6f);
                }
            }
        }
    }
}
