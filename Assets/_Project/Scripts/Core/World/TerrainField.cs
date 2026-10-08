using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChroniclesOfRebirth.Core.World
{
    /// <summary>Тип поверхности клетки рельефа. Значение — индекс слоя в массиве текстур земли.</summary>
    public enum GroundType : byte
    {
        DeepBed,
        ShallowBed,
        Pebbles,
        Grass,
        GrassLeaves,
        ForestFloor,
        Dirt,
        Flagstone,
        Rock,
        Snow,
    }

    /// <summary>
    /// Рельеф — непрерывная карта высот: вершины через <see cref="VertexSpacing"/> юнита, между ними — два треугольника
    /// (диагональ от (i, j) к (i + 1, j + 1)). Склоны плавные, обрывы — просто очень крутые участки той же поверхности;
    /// шейдер земли сам кладёт на них текстуру обрыва. Типы земли хранятся на более мелкой сетке клеток
    /// (<see cref="CellSize"/>), из неё строится карта для шейдера смешивания.
    /// </summary>
    public sealed class TerrainField
    {
        public const float CellSize = 0.25f;
        public const int CellsPerUnit = 4;
        public const float VertexSpacing = 0.5f;
        public const int VerticesPerUnit = 2;

        readonly float[] _heights;
        readonly GroundType[] _types;

        /// <param name="minX">Мировая X левого края (в юнитах, целое).</param>
        /// <param name="minZ">Мировая Z нижнего края (в юнитах, целое).</param>
        /// <param name="widthUnits">Ширина в юнитах.</param>
        /// <param name="depthUnits">Глубина в юнитах.</param>
        /// <param name="bottomHeight">Низ «юбки» по краю мира.</param>
        public TerrainField(int minX, int minZ, int widthUnits, int depthUnits, float bottomHeight)
        {
            MinX = minX;
            MinZ = minZ;
            Columns = widthUnits * CellsPerUnit;
            Rows = depthUnits * CellsPerUnit;
            VertexColumns = widthUnits * VerticesPerUnit + 1;
            VertexRows = depthUnits * VerticesPerUnit + 1;
            BottomHeight = bottomHeight;
            _heights = new float[VertexColumns * VertexRows];
            _types = new GroundType[Columns * Rows];
        }

        public int MinX { get; }
        public int MinZ { get; }
        public int Columns { get; }
        public int Rows { get; }
        public int VertexColumns { get; }
        public int VertexRows { get; }
        public float BottomHeight { get; }
        public float MaxX => MinX + Columns * CellSize;
        public float MaxZ => MinZ + Rows * CellSize;

        // ---------- Клетки (типы земли) ----------

        int Index(int c, int r) => r * Columns + c;

        public bool InBounds(int c, int r) => c >= 0 && r >= 0 && c < Columns && r < Rows;

        /// <summary>Центр клетки в мировых координатах.</summary>
        public Vector2 CellCenter(int c, int r) => new(MinX + (c + 0.5f) * CellSize, MinZ + (r + 0.5f) * CellSize);

        public bool TryCell(float x, float z, out int c, out int r)
        {
            c = Mathf.FloorToInt((x - MinX) / CellSize);
            r = Mathf.FloorToInt((z - MinZ) / CellSize);
            return InBounds(c, r);
        }

        public GroundType GetGround(int c, int r) => _types[Index(c, r)];

        public void SetGround(int c, int r, GroundType type) => _types[Index(c, r)] = type;

        public GroundType SampleGround(float x, float z)
        {
            TryCell(x, z, out var c, out var r);
            return GetGround(Mathf.Clamp(c, 0, Columns - 1), Mathf.Clamp(r, 0, Rows - 1));
        }

        /// <summary>Высота поверхности в центре клетки.</summary>
        public float GetHeight(int c, int r)
        {
            var p = CellCenter(c, r);
            return SampleHeight(p.x, p.y);
        }

        // ---------- Вершины (высоты) ----------

        int VertexIndex(int i, int j) => j * VertexColumns + i;

        public bool VertexInBounds(int i, int j) => i >= 0 && j >= 0 && i < VertexColumns && j < VertexRows;

        public Vector2 VertexPosition(int i, int j) => new(MinX + i * VertexSpacing, MinZ + j * VertexSpacing);

        public float GetVertexHeight(int i, int j) =>
            _heights[VertexIndex(Mathf.Clamp(i, 0, VertexColumns - 1), Mathf.Clamp(j, 0, VertexRows - 1))];

        public void SetVertexHeight(int i, int j, float height) => _heights[VertexIndex(i, j)] = height;

        /// <summary>Ближайшая вершина к мировой точке.</summary>
        public void NearestVertex(float x, float z, out int i, out int j)
        {
            i = Mathf.Clamp(Mathf.RoundToInt((x - MinX) / VertexSpacing), 0, VertexColumns - 1);
            j = Mathf.Clamp(Mathf.RoundToInt((z - MinZ) / VertexSpacing), 0, VertexRows - 1);
        }

        /// <summary>
        /// Высота поверхности под мировой точкой — точно по треугольникам меша (то, на чём стоит герой).
        /// </summary>
        public float SampleHeight(float x, float z)
        {
            var gx = Mathf.Clamp((x - MinX) / VertexSpacing, 0f, VertexColumns - 1.001f);
            var gz = Mathf.Clamp((z - MinZ) / VertexSpacing, 0f, VertexRows - 1.001f);
            var i = Mathf.FloorToInt(gx);
            var j = Mathf.FloorToInt(gz);
            var fx = gx - i;
            var fz = gz - j;
            var h00 = _heights[VertexIndex(i, j)];
            var h10 = _heights[VertexIndex(i + 1, j)];
            var h01 = _heights[VertexIndex(i, j + 1)];
            var h11 = _heights[VertexIndex(i + 1, j + 1)];
            return fx > fz
                ? h00 + (h10 - h00) * fx + (h11 - h10) * fz
                : h00 + (h11 - h01) * fx + (h01 - h00) * fz;
        }

        /// <summary>Нормаль вершины по центральным разностям.</summary>
        public Vector3 VertexNormal(int i, int j)
        {
            var dx = GetVertexHeight(i + 1, j) - GetVertexHeight(i - 1, j);
            var dz = GetVertexHeight(i, j + 1) - GetVertexHeight(i, j - 1);
            return new Vector3(-dx, 2f * VertexSpacing, -dz).normalized;
        }

        /// <summary>Сглаженная нормаль поверхности в мировой точке (билинейно по нормалям вершин).</summary>
        public Vector3 SampleNormal(float x, float z)
        {
            var gx = Mathf.Clamp((x - MinX) / VertexSpacing, 0f, VertexColumns - 1.001f);
            var gz = Mathf.Clamp((z - MinZ) / VertexSpacing, 0f, VertexRows - 1.001f);
            var i = Mathf.FloorToInt(gx);
            var j = Mathf.FloorToInt(gz);
            var fx = gx - i;
            var fz = gz - j;
            var n = Vector3.Lerp(Vector3.Lerp(VertexNormal(i, j), VertexNormal(i + 1, j), fx),
                Vector3.Lerp(VertexNormal(i, j + 1), VertexNormal(i + 1, j + 1), fx), fz);
            return n.normalized;
        }

        /// <summary>Крутизна склона в градусах.</summary>
        public float SampleSlope(float x, float z) => Vector3.Angle(SampleNormal(x, z), Vector3.up);

        /// <summary>
        /// Сглаживание высот: несколько проходов усреднения по соседям (крест 3×3). weight(i, j) ∈ [0, 1] — насколько
        /// вершина поддаётся (0 — закреплена: плато, ступени, русло).
        /// </summary>
        public void Smooth(int passes, Func<int, int, float> weight)
        {
            var copy = new float[_heights.Length];
            for (var pass = 0; pass < passes; pass++)
            {
                Array.Copy(_heights, copy, _heights.Length);
                for (var j = 1; j < VertexRows - 1; j++)
                for (var i = 1; i < VertexColumns - 1; i++)
                {
                    var w = weight(i, j);
                    if (w <= 0f)
                        continue;
                    var k = VertexIndex(i, j);
                    var average = (copy[k] * 4f + copy[k - 1] + copy[k + 1] + copy[k - VertexColumns] + copy[k + VertexColumns]) / 8f;
                    _heights[k] = Mathf.Lerp(copy[k], average, w);
                }
            }
        }

        // ---------- Карта типов земли ----------

        /// <summary>
        /// Карта типов земли для шейдеров земли и травы: пиксель на клетку, R = тип / 16.
        /// Фильтрация Point — шейдеры сами делают неровные границы, сдвигая точку выборки шумом.
        /// </summary>
        public Texture2D BuildGroundMap()
        {
            var data = new byte[Columns * Rows];
            for (var i = 0; i < data.Length; i++)
                data[i] = (byte)Mathf.RoundToInt((int)_types[i] / 16f * 255f);

            var texture = new Texture2D(Columns, Rows, TextureFormat.R8, false, true)
            {
                name = "GroundMap",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixelData(data, 0);
            texture.Apply(false, false);
            return texture;
        }

        // ---------- Меши ----------

        /// <summary>Участки рельефа: квадраты по <paramref name="chunkUnits"/> юнитов в индексах вершин [i0, i1) × [j0, j1).</summary>
        public IEnumerable<RectInt> Chunks(int chunkUnits)
        {
            var step = chunkUnits * VerticesPerUnit;
            for (var j = 0; j < VertexRows - 1; j += step)
            for (var i = 0; i < VertexColumns - 1; i += step)
                yield return new RectInt(i, j, Mathf.Min(step, VertexColumns - 1 - i), Mathf.Min(step, VertexRows - 1 - j));
        }

        /// <summary>
        /// Поверхность участка (квады между вершинами <paramref name="quads"/>), общие вершины и гладкие нормали.
        /// includeQuad(i, j) — брать ли квад с левым нижним углом (i, j) (null — все); vertexColor(i, j) — цвет вершины
        /// (данные для шейдера, null — белый). skirt — опустить «юбку» до <see cref="BottomHeight"/> по краю мира.
        /// Возвращает null, если в участке нет ни одного квада.
        /// </summary>
        public Mesh BuildSurface(string name, RectInt quads, Func<int, int, bool> includeQuad = null,
            Func<int, int, Color32> vertexColor = null, bool skirt = false)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color32>();
            var triangles = new List<int>();
            var map = new Dictionary<int, int>();
            var white = new Color32(255, 255, 255, 255);

            int Vertex(int i, int j)
            {
                var key = VertexIndex(i, j);
                if (map.TryGetValue(key, out var index))
                    return index;
                var p = VertexPosition(i, j);
                vertices.Add(new Vector3(p.x, _heights[key], p.y));
                normals.Add(VertexNormal(i, j));
                colors.Add(vertexColor?.Invoke(i, j) ?? white);
                map[key] = vertices.Count - 1;
                return vertices.Count - 1;
            }

            for (var j = quads.yMin; j < quads.yMax; j++)
            for (var i = quads.xMin; i < quads.xMax; i++)
            {
                if (includeQuad != null && !includeQuad(i, j))
                    continue;
                // a — (i, j), b — (i, j + 1), c — (i + 1, j + 1), d — (i + 1, j); лицом вверх, диагональ a–c.
                int a = Vertex(i, j), b = Vertex(i, j + 1), c = Vertex(i + 1, j + 1), d = Vertex(i + 1, j);
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }

            if (triangles.Count == 0)
                return null;

            if (skirt)
                AddSkirt(quads, vertices, normals, colors, triangles);

            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            return mesh;
        }

        /// <summary>Вертикальные стенки от края мира вниз: со стороны камеры у мира есть толщина, а не «бумажный» край.</summary>
        void AddSkirt(RectInt quads, List<Vector3> vertices, List<Vector3> normals, List<Color32> colors, List<int> triangles)
        {
            var white = new Color32(255, 255, 255, 255);

            void Wall(int i0, int j0, int i1, int j1, Vector3 outward)
            {
                var p0 = VertexPosition(i0, j0);
                var p1 = VertexPosition(i1, j1);
                var start = vertices.Count;
                vertices.Add(new Vector3(p0.x, GetVertexHeight(i0, j0), p0.y));
                vertices.Add(new Vector3(p1.x, GetVertexHeight(i1, j1), p1.y));
                vertices.Add(new Vector3(p1.x, BottomHeight, p1.y));
                vertices.Add(new Vector3(p0.x, BottomHeight, p0.y));
                for (var k = 0; k < 4; k++)
                {
                    normals.Add(outward);
                    colors.Add(white);
                }

                // Лицом наружу: лицевая сторона треугольника (a, b, c) смотрит по Cross(b − a, c − a).
                var faceNormal = Vector3.Cross(vertices[start + 1] - vertices[start], vertices[start + 2] - vertices[start]);
                if (Vector3.Dot(faceNormal, outward) > 0f)
                {
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
                else
                {
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                    triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
                }
            }

            if (quads.yMin == 0)
                for (var i = quads.xMin; i < quads.xMax; i++)
                    Wall(i, 0, i + 1, 0, Vector3.back);
            if (quads.yMax == VertexRows - 1)
                for (var i = quads.xMin; i < quads.xMax; i++)
                    Wall(i, VertexRows - 1, i + 1, VertexRows - 1, Vector3.forward);
            if (quads.xMin == 0)
                for (var j = quads.yMin; j < quads.yMax; j++)
                    Wall(0, j, 0, j + 1, Vector3.left);
            if (quads.xMax == VertexColumns - 1)
                for (var j = quads.yMin; j < quads.yMax; j++)
                    Wall(VertexColumns - 1, j, VertexColumns - 1, j + 1, Vector3.right);
        }
    }
}
