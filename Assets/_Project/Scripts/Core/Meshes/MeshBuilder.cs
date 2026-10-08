using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChroniclesOfRebirth.Core.Meshes
{
    /// <summary>
    /// Сборщик мешей из примитивов. UV по умолчанию — мировые (box-проекция),
    /// чтобы плотность пикселей была одинаковой: 1 юнит = 1/<see cref="UvUnitsPerTile"/> тайла.
    /// </summary>
    public sealed class MeshBuilder
    {
        /// <summary>Сколько юнитов мира покрывает одна текстура 64 px при 16 px/юнит.</summary>
        public const float UvUnitsPerTile = 4f;

        readonly List<Vector3> _vertices = new();
        readonly List<Vector3> _normals = new();
        readonly List<Vector2> _uv0 = new();
        readonly List<Vector4> _uv1 = new();
        readonly List<Vector4> _uv2 = new();
        readonly List<Color32> _colors = new();
        readonly List<List<int>> _submeshes = new();
        float _billboardExtent;

        public int VertexCount => _vertices.Count;

        /// <summary>
        /// «Якорь» для следующих билбордов (UV2): xyz — центр кроны, общий для всех её клубков, w — радиус кроны.
        /// Если не задан, якорем служит pivot билборда с нулевым радиусом.
        /// </summary>
        public Vector4? Anchor { get; set; }

        List<int> Submesh(int index)
        {
            while (_submeshes.Count <= index)
                _submeshes.Add(new List<int>());
            return _submeshes[index];
        }

        int AddVertex(Vector3 position, Vector3 normal, Vector2 uv, Vector4 uv1, Color32 color, Vector4 anchor = default)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _uv0.Add(uv);
            _uv1.Add(uv1);
            _uv2.Add(anchor);
            _colors.Add(color);
            return _vertices.Count - 1;
        }

        /// <summary>Плоский четырёхугольник (вершины по часовой стрелке, если смотреть с лицевой стороны).</summary>
        public void AddQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Color32 color)
        {
            var normal = QuadNormal(a, b, c, d);
            var i = AddVertex(a, normal, uvA, Vector2.zero, color);
            AddVertex(b, normal, uvB, Vector2.zero, color);
            AddVertex(c, normal, uvC, Vector2.zero, color);
            AddVertex(d, normal, uvD, Vector2.zero, color);
            var tris = Submesh(submesh);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        public void AddTriangle(int submesh, Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            var i = AddVertex(a, normal, BoxUv(a, normal), Vector2.zero, color);
            AddVertex(b, normal, BoxUv(b, normal), Vector2.zero, color);
            AddVertex(c, normal, BoxUv(c, normal), Vector2.zero, color);
            var tris = Submesh(submesh);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// <summary>Нормаль по диагоналям — устойчива и для вырожденных четырёхугольников (треугольников).</summary>
        static Vector3 QuadNormal(Vector3 a, Vector3 b, Vector3 c, Vector3 d) => Vector3.Cross(c - a, d - b).normalized;

        /// <summary>Четырёхугольник с мировыми UV по box-проекции.</summary>
        public void AddQuadWorldUv(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            var normal = QuadNormal(a, b, c, d);
            AddQuad(submesh, a, b, c, d, BoxUv(a, normal), BoxUv(b, normal), BoxUv(c, normal), BoxUv(d, normal), color);
        }

        public static Vector2 BoxUv(Vector3 p, Vector3 normal)
        {
            var n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            Vector2 uv;
            if (n.y >= n.x && n.y >= n.z)
                uv = new Vector2(p.x, p.z);
            else if (n.x >= n.z)
                uv = new Vector2(p.z, p.y);
            else
                uv = new Vector2(p.x, p.y);
            return uv / UvUnitsPerTile;
        }

        /// <summary>Коробка, преобразованная матрицей; UV мировые.</summary>
        public void AddBox(int submesh, Matrix4x4 transform, Vector3 size, Color32 color, bool skipBottom = true)
        {
            var h = size * 0.5f;
            var p = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                var local = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                p[i] = transform.MultiplyPoint3x4(local);
            }

            AddQuadWorldUv(submesh, p[2], p[6], p[7], p[3], color); // верх
            AddQuadWorldUv(submesh, p[0], p[2], p[3], p[1], color); // -z
            AddQuadWorldUv(submesh, p[5], p[7], p[6], p[4], color); // +z
            AddQuadWorldUv(submesh, p[4], p[6], p[2], p[0], color); // -x
            AddQuadWorldUv(submesh, p[1], p[3], p[7], p[5], color); // +x
            if (!skipBottom)
                AddQuadWorldUv(submesh, p[4], p[0], p[1], p[5], color);
        }

        /// <summary>
        /// Призма/усечённый конус вдоль локальной оси Y. topJitter делает «обломанный» верх.
        /// UV: u — по окружности, v — по высоте, в масштабе мира.
        /// </summary>
        public void AddPrism(int submesh, Matrix4x4 transform, int sides, float bottomRadius, float topRadius,
            float height, Color32 color, bool cap = true, float[] topJitter = null)
        {
            var circumference = Mathf.PI * (bottomRadius + topRadius);
            for (var i = 0; i < sides; i++)
            {
                var a0 = i / (float)sides * Mathf.PI * 2f;
                var a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var h0 = height + (topJitter != null ? topJitter[i % topJitter.Length] : 0f);
                var h1 = height + (topJitter != null ? topJitter[(i + 1) % topJitter.Length] : 0f);

                var b0 = transform.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0) * bottomRadius, 0f, Mathf.Sin(a0) * bottomRadius));
                var b1 = transform.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1) * bottomRadius, 0f, Mathf.Sin(a1) * bottomRadius));
                var t0 = transform.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0) * topRadius, h0, Mathf.Sin(a0) * topRadius));
                var t1 = transform.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1) * topRadius, h1, Mathf.Sin(a1) * topRadius));

                var u0 = i / (float)sides * circumference / UvUnitsPerTile;
                var u1 = (i + 1) / (float)sides * circumference / UvUnitsPerTile;
                AddQuad(submesh, b0, t0, t1, b1,
                    new Vector2(u0, 0f), new Vector2(u0, h0 / UvUnitsPerTile),
                    new Vector2(u1, h1 / UvUnitsPerTile), new Vector2(u1, 0f), color);
            }

            if (!cap)
                return;

            var center = transform.MultiplyPoint3x4(new Vector3(0f, height, 0f));
            for (var i = 0; i < sides; i++)
            {
                var a0 = i / (float)sides * Mathf.PI * 2f;
                var a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var h0 = height + (topJitter != null ? topJitter[i % topJitter.Length] : 0f);
                var h1 = height + (topJitter != null ? topJitter[(i + 1) % topJitter.Length] : 0f);
                var t0 = transform.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0) * topRadius, h0, Mathf.Sin(a0) * topRadius));
                var t1 = transform.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1) * topRadius, h1, Mathf.Sin(a1) * topRadius));
                AddTriangle(submesh, center, t1, t0, color);
            }
        }

        /// <summary>Единичные сферы по уровню подразделения икосаэдра: вершины и треугольники.</summary>
        static readonly Dictionary<int, (List<Vector3> vertices, List<int> triangles)> Spheres = new();

        /// <summary>
        /// Округлый неровный валун: подразделённый икосаэдр (detail — число подразделений: 2 для валунов,
        /// 0–1 для гальки), форма — несколько крупных «наплывов» в случайных направлениях, плоское основание
        /// (камень сидит в земле). Нормали сглажены — свет ложится мягко; UV — по нормали каждого треугольника,
        /// текстура не растягивается на изгибах.
        /// </summary>
        public void AddRock(int submesh, Vector3 position, Vector3 scale, float rotationY, int seed, Color32 color, int detail = 2)
        {
            var (sphereVertices, sphereTriangles) = Sphere(detail);
            var m = Matrix4x4.TRS(position, Quaternion.Euler(0f, rotationY, 0f), scale);

            // Наплывы: направление и сила; вместе с мелкой неровностью дают уникальную форму каждому камню.
            var lumps = new Vector4[5];
            for (var k = 0; k < lumps.Length; k++)
            {
                var dir = new Vector3(Pixel.PixelNoise.Hash01(k, seed, 81) - 0.5f, Pixel.PixelNoise.Hash01(k, seed, 82) - 0.3f,
                    Pixel.PixelNoise.Hash01(k, seed, 83) - 0.5f).normalized;
                lumps[k] = new Vector4(dir.x, dir.y, dir.z, 0.18f + Pixel.PixelNoise.Hash01(k, seed, 84) * 0.4f);
            }

            // Срезы: несколько плоскостей «скалывают» камень — грани крупные и плавные, а не шар.
            var cuts = new Vector4[3];
            for (var k = 0; k < cuts.Length; k++)
            {
                var dir = new Vector3(Pixel.PixelNoise.Hash01(k, seed, 86) - 0.5f, Pixel.PixelNoise.Hash01(k, seed, 87) * 0.8f,
                    Pixel.PixelNoise.Hash01(k, seed, 88) - 0.5f).normalized;
                cuts[k] = new Vector4(dir.x, dir.y, dir.z, 0.62f + Pixel.PixelNoise.Hash01(k, seed, 89) * 0.25f);
            }

            var points = new Vector3[sphereVertices.Count];
            for (var i = 0; i < points.Length; i++)
            {
                var d = sphereVertices[i];
                var r = 0.7f;
                foreach (var lump in lumps)
                {
                    var along = Mathf.Max(0f, Vector3.Dot(d, new Vector3(lump.x, lump.y, lump.z)));
                    r += lump.w * along * along;
                }
                r += (Pixel.PixelNoise.Hash01(i, seed, 85) - 0.5f) * 0.05f;
                var p = d * r;
                foreach (var cut in cuts)
                {
                    var n = new Vector3(cut.x, cut.y, cut.z);
                    var over = Vector3.Dot(p, n) - cut.w;
                    if (over > 0f)
                        p -= n * over * 0.85f;
                }
                p.y = Mathf.Max(p.y, -0.1f); // плоское основание: нижняя часть камня в земле
                points[i] = m.MultiplyPoint3x4(p);
            }

            // Сглаженные нормали вершин — сумма нормалей прилегающих граней.
            var normals = new Vector3[points.Length];
            for (var i = 0; i < sphereTriangles.Count; i += 3)
            {
                int a = sphereTriangles[i], b = sphereTriangles[i + 1], c = sphereTriangles[i + 2];
                var n = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                normals[a] += n;
                normals[b] += n;
                normals[c] += n;
            }

            var tris = Submesh(submesh);
            for (var i = 0; i < sphereTriangles.Count; i += 3)
            {
                int a = sphereTriangles[i], b = sphereTriangles[i + 1], c = sphereTriangles[i + 2];
                var face = Vector3.Cross(points[b] - points[a], points[c] - points[a]).normalized;
                var start = _vertices.Count;
                foreach (var v in new[] { a, b, c })
                    AddVertex(points[v], normals[v].normalized, BoxUv(points[v], face), Vector2.zero, color);
                tris.Add(start);
                tris.Add(start + 1);
                tris.Add(start + 2);
            }
        }

        /// <summary>Единичная сфера: икосаэдр, подразделённый levels раз (2 — 162 вершины, 320 треугольников), грани наружу.</summary>
        static (List<Vector3> vertices, List<int> triangles) Sphere(int levels)
        {
            if (Spheres.TryGetValue(levels, out var cached))
                return cached;

            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var vertices = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (var i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;
            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (var level = 0; level < levels; level++)
            {
                var midpoints = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (midpoints.TryGetValue(key, out var index))
                        return index;
                    vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
                    midpoints[key] = vertices.Count - 1;
                    return vertices.Count - 1;
                }

                var next = new List<int>(triangles.Count * 4);
                for (var i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = next;
            }

            // Ориентация: грань должна смотреть наружу от центра.
            for (var i = 0; i < triangles.Count; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }

            Spheres[levels] = (vertices, triangles);
            return (vertices, triangles);
        }

        /// <summary>
        /// Трубка вдоль ломаной (ствол, ветвь, корень): кольца по <paramref name="sides"/> вершин, нормали гладкие —
        /// по реальной поверхности, поэтому наплывы и сужения освещаются верно. Шов развёртки смотрит на +Z (от камеры).
        /// UV: u — по окружности (по среднему радиусу), v — длина вдоль оси со сдвигом vOffset; масштаб мировой.
        /// Для шейдера коры (PixelBark) — локальная система ветви, рисунок не зависит от того, где дерево стоит в мире:
        /// UV1 — направление оси в кольце (xyz) и длина вдоль оси от начала трубки (w);
        /// UV2 — центр кольца (xyz) и сдвиг рисунка vOffset (w), свой у каждой ветви.
        /// radialScale(кольцо, угол) — множитель радиуса для наплывов у комля.
        /// </summary>
        public void AddTube(int submesh, IReadOnlyList<Vector3> points, IReadOnlyList<float> radii, int sides, Color32 color,
            float vOffset = 0f, System.Func<int, float, float> radialScale = null)
        {
            var rings = points.Count;
            if (rings < 2)
                return;

            // Касательные и опорные оси колец (параллельный перенос — без закрутки на изгибах).
            var tangents = new Vector3[rings];
            for (var i = 0; i < rings; i++)
            {
                var prev = points[Mathf.Max(0, i - 1)];
                var next = points[Mathf.Min(rings - 1, i + 1)];
                tangents[i] = (next - prev).normalized;
            }

            var axis = Vector3.ProjectOnPlane(Vector3.forward, tangents[0]);
            if (axis.sqrMagnitude < 1e-4f)
                axis = Vector3.ProjectOnPlane(Vector3.right, tangents[0]);
            axis.Normalize();

            var grid = new Vector3[rings, sides + 1];
            var meanRadius = 0f;
            for (var i = 0; i < rings; i++)
            {
                if (i > 0)
                    axis = Vector3.ProjectOnPlane(Quaternion.FromToRotation(tangents[i - 1], tangents[i]) * axis, tangents[i]).normalized;
                var binormal = Vector3.Cross(tangents[i], axis);
                meanRadius += radii[i] / rings;
                for (var j = 0; j <= sides; j++)
                {
                    var angle = j / (float)sides * Mathf.PI * 2f;
                    var r = radii[i] * (radialScale?.Invoke(i, angle) ?? 1f);
                    grid[i, j] = points[i] + (axis * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * r;
                }
            }

            var uScale = Mathf.PI * 2f * meanRadius / UvUnitsPerTile;
            var start = _vertices.Count;
            var length = 0f;
            for (var i = 0; i < rings; i++)
            {
                if (i > 0)
                    length += Vector3.Distance(points[i], points[i - 1]);
                var axisData = new Vector4(tangents[i].x, tangents[i].y, tangents[i].z, length);
                var ringData = new Vector4(points[i].x, points[i].y, points[i].z, vOffset);
                for (var j = 0; j <= sides; j++)
                {
                    // Нормаль по соседям сетки; направление — наружу от оси.
                    var around = grid[i, (j + 1) % sides] - grid[i, (j + sides - 1) % sides];
                    var along = grid[Mathf.Min(rings - 1, i + 1), j] - grid[Mathf.Max(0, i - 1), j];
                    var normal = Vector3.Cross(along, around).normalized;
                    if (Vector3.Dot(normal, grid[i, j] - points[i]) < 0f)
                        normal = -normal;
                    AddVertex(grid[i, j], normal, new Vector2(j / (float)sides * uScale, vOffset + length / UvUnitsPerTile),
                        axisData, color, ringData);
                }
            }

            var tris = Submesh(submesh);
            var stride = sides + 1;
            for (var i = 0; i < rings - 1; i++)
            for (var j = 0; j < sides; j++)
            {
                var a = start + i * stride + j;
                var b = a + stride;
                // Порядок обхода: лицевая сторона наружу (угол растёт от +Z к +X).
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
            }
        }

        /// <summary>
        /// Плоская карточка в 3D (пучок листьев): центр, полуоси right/up, нормаль для освещения (обычно «сферическая»
        /// от центра кроны). UV1: xyz — центр карточки, w — сила ветра; UV2 — якорь кроны (<see cref="Anchor"/>).
        /// </summary>
        public void AddCard(int submesh, Vector3 center, Vector3 right, Vector3 up, Vector3 normal, Rect uvRect, Color32 color, float wind)
        {
            var anchor = Anchor ?? new Vector4(center.x, center.y, center.z, 0f);
            var data = new Vector4(center.x, center.y, center.z, wind);
            var i = AddVertex(center - right - up, normal, new Vector2(uvRect.xMin, uvRect.yMin), data, color, anchor);
            AddVertex(center - right + up, normal, new Vector2(uvRect.xMin, uvRect.yMax), data, color, anchor);
            AddVertex(center + right + up, normal, new Vector2(uvRect.xMax, uvRect.yMax), data, color, anchor);
            AddVertex(center + right - up, normal, new Vector2(uvRect.xMax, uvRect.yMin), data, color, anchor);
            var tris = Submesh(submesh);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>
        /// Вертикальная карточка растения (стебли, папоротник), стоящая на земле: низ — на земле, верх качается.
        /// Для шейдера листвы (PixelFoliage): UV1 — центр карточки и сила ветра (у нижних вершин 0 — корни на месте),
        /// UV2 — якорь (центр карточки, радиус 0: растение не бледнеет вместе с кронами).
        /// </summary>
        public void AddPlantCard(int submesh, Vector3 bottomCenter, Vector3 halfRight, float height, Vector3 normal, Rect uvRect,
            Color32 color, float wind)
        {
            var up = Vector3.up * height;
            var center = bottomCenter + up * 0.5f;
            var anchor = new Vector4(center.x, center.y, center.z, 0f);
            var rooted = new Vector4(center.x, center.y, center.z, 0f);
            var swaying = new Vector4(center.x, center.y, center.z, wind);
            var i = AddVertex(bottomCenter - halfRight, normal, new Vector2(uvRect.xMin, uvRect.yMin), rooted, color, anchor);
            AddVertex(bottomCenter - halfRight + up, normal, new Vector2(uvRect.xMin, uvRect.yMax), swaying, color, anchor);
            AddVertex(bottomCenter + halfRight + up, normal, new Vector2(uvRect.xMax, uvRect.yMax), swaying, color, anchor);
            AddVertex(bottomCenter + halfRight, normal, new Vector2(uvRect.xMax, uvRect.yMin), rooted, color, anchor);
            var tris = Submesh(submesh);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>
        /// Билборд: 4 вершины в точке pivot, смещения углов (в юнитах) — в UV1.
        /// Разворот к камере делает шейдер PixelSprite. Pivot — середина нижнего края.
        /// </summary>
        public void AddBillboard(int submesh, Vector3 pivot, Vector2 size, Rect uvRect, Color32 color, float pivotY = 0f)
        {
            var hw = size.x * 0.5f;
            var bottom = -size.y * pivotY;
            var top = bottom + size.y;
            var anchor = Anchor ?? new Vector4(pivot.x, pivot.y, pivot.z, 0f);
            var i = AddVertex(pivot, Vector3.up, new Vector2(uvRect.xMin, uvRect.yMin), new Vector2(-hw, bottom), color, anchor);
            AddVertex(pivot, Vector3.up, new Vector2(uvRect.xMin, uvRect.yMax), new Vector2(-hw, top), color, anchor);
            AddVertex(pivot, Vector3.up, new Vector2(uvRect.xMax, uvRect.yMax), new Vector2(hw, top), color, anchor);
            AddVertex(pivot, Vector3.up, new Vector2(uvRect.xMax, uvRect.yMin), new Vector2(hw, bottom), color, anchor);
            var tris = Submesh(submesh);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            _billboardExtent = Mathf.Max(_billboardExtent, Mathf.Max(hw, Mathf.Abs(top), Mathf.Abs(bottom)));
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uv0);
            mesh.SetUVs(1, _uv1);
            mesh.SetUVs(2, _uv2);
            mesh.SetColors(_colors);
            mesh.subMeshCount = Mathf.Max(1, _submeshes.Count);
            for (var i = 0; i < _submeshes.Count; i++)
                mesh.SetTriangles(_submeshes[i], i, false);
            mesh.RecalculateBounds();

            // Вершины билбордов лежат в одной точке — расширяем bounds на размер спрайтов, иначе отсечение ошибётся.
            if (_billboardExtent > 0f)
            {
                var b = mesh.bounds;
                b.Expand(_billboardExtent * 2f + 1f);
                mesh.bounds = b;
            }

            return mesh;
        }
    }
}
