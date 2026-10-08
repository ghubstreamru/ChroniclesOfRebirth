using ChroniclesOfRebirth.Core.Meshes;
using UnityEngine;

namespace ChroniclesOfRebirth.Core.World
{
    /// <summary>Тип поверхности клетки рельефа. Значение — индекс сабмеша верхних граней.</summary>
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
    /// Рельеф-диорама на мелкой сетке (<see cref="CellSize"/> юнита). Большинство клеток — плоские террасы
    /// с высотой, кратной <see cref="LevelHeight"/>: их очертания задаются плавными формами с шумом, поэтому
    /// обрывы получаются кривыми, а не прямоугольными. «Гладкие» клетки (тропы, пандусы) имеют произвольную
    /// высоту и сглаживаются с соседями — получаются наклонные поверхности.
    /// </summary>
    public sealed class TerrainField
    {
        public const float LevelHeight = 0.5f;
        public const float CellSize = 0.25f;
        public const int CellsPerUnit = 4;

        /// <summary>Сабмеши боковин: земляной обрыв, каменная кладка и горная скала.</summary>
        public const int SideEarth = 10, SideStone = 11, SideRock = 12, SubmeshCount = 13;

        readonly float[] _heights;
        readonly bool[] _smooth;
        readonly bool[] _rockyCliff;
        readonly GroundType[] _types;

        /// <param name="minX">Мировая X левого края (в юнитах, целое).</param>
        /// <param name="minZ">Мировая Z нижнего края (в юнитах, целое).</param>
        /// <param name="widthUnits">Ширина в юнитах.</param>
        /// <param name="depthUnits">Глубина в юнитах.</param>
        public TerrainField(int minX, int minZ, int widthUnits, int depthUnits, float bottomHeight)
        {
            MinX = minX;
            MinZ = minZ;
            Columns = widthUnits * CellsPerUnit;
            Rows = depthUnits * CellsPerUnit;
            BottomHeight = bottomHeight;
            _heights = new float[Columns * Rows];
            _smooth = new bool[Columns * Rows];
            _rockyCliff = new bool[Columns * Rows];
            _types = new GroundType[Columns * Rows];
        }

        public int MinX { get; }
        public int MinZ { get; }
        public int Columns { get; }
        public int Rows { get; }
        public float BottomHeight { get; }
        public float MaxX => MinX + Columns * CellSize;
        public float MaxZ => MinZ + Rows * CellSize;

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

        public float GetHeight(int c, int r) => _heights[Index(c, r)];
        public bool IsSmooth(int c, int r) => _smooth[Index(c, r)];
        public GroundType GetGround(int c, int r) => _types[Index(c, r)];

        /// <summary>Плоская клетка на целом уровне.</summary>
        public void SetLevel(int c, int r, int level, GroundType type)
        {
            var i = Index(c, r);
            _heights[i] = level * LevelHeight;
            _smooth[i] = false;
            _types[i] = type;
        }

        /// <summary>Гладкая клетка с произвольной высотой (тропа, пандус).</summary>
        public void SetSmooth(int c, int r, float height, GroundType type)
        {
            var i = Index(c, r);
            _heights[i] = height;
            _smooth[i] = true;
            _types[i] = type;
        }

        public void SetGround(int c, int r, GroundType type) => _types[Index(c, r)] = type;

        /// <summary>Обрывы под клеткой — горная скала вместо земли (для гор, где поверхность сверху ещё травяная).</summary>
        public void SetRockyCliff(int c, int r, bool rocky) => _rockyCliff[Index(c, r)] = rocky;

        /// <summary>Высота поверхности под мировой точкой.</summary>
        public float SampleHeight(float x, float z)
        {
            TryCell(x, z, out var c, out var r);
            c = Mathf.Clamp(c, 0, Columns - 1);
            r = Mathf.Clamp(r, 0, Rows - 1);
            return GetHeight(c, r);
        }

        public GroundType SampleGround(float x, float z)
        {
            TryCell(x, z, out var c, out var r);
            return GetGround(Mathf.Clamp(c, 0, Columns - 1), Mathf.Clamp(r, 0, Rows - 1));
        }

        float HeightOrOwn(int c, int r, float own) => InBounds(c, r) ? GetHeight(c, r) : own;

        /// <summary>
        /// Высота угла клетки. dx, dz ∈ {0, 1}. Плоские клетки — ровные; гладкие усредняют четыре клетки вокруг угла.
        /// </summary>
        float CornerHeight(int c, int r, int dx, int dz)
        {
            var own = GetHeight(c, r);
            if (!IsSmooth(c, r))
                return own;

            var c0 = c + dx - 1;
            var r0 = r + dz - 1;
            return (HeightOrOwn(c0, r0, own) + HeightOrOwn(c0 + 1, r0, own) +
                    HeightOrOwn(c0, r0 + 1, own) + HeightOrOwn(c0 + 1, r0 + 1, own)) * 0.25f;
        }

        /// <summary>
        /// Карта типов земли для шейдера смешивания (TerrainBlend): пиксель на клетку, R = тип / 16.
        /// Фильтрация Point — шейдер сам делает неровные границы, сдвигая точку выборки шумом.
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

        public Mesh BuildMesh(string name)
        {
            var builder = new MeshBuilder();
            var white = new Color32(255, 255, 255, 255);
            BuildFlatTops(builder, white);
            BuildSmoothTops(builder, white);
            BuildSides(builder, white);
            return builder.Build(name);
        }

        /// <summary>Плоские клетки объединяются в прямоугольники (greedy meshing) — меньше вершин.</summary>
        void BuildFlatTops(MeshBuilder builder, Color32 color)
        {
            var used = new bool[Columns * Rows];
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Columns; c++)
            {
                var i = Index(c, r);
                if (used[i] || _smooth[i])
                    continue;

                var h = _heights[i];
                var type = _types[i];
                var w = 1;
                while (c + w < Columns && Matches(c + w, r, h, type, used))
                    w++;

                var d = 1;
                while (r + d < Rows && RowMatches(c, r + d, w, h, type, used))
                    d++;

                for (var rr = r; rr < r + d; rr++)
                for (var cc = c; cc < c + w; cc++)
                    used[Index(cc, rr)] = true;

                var x0 = MinX + c * CellSize;
                var z0 = MinZ + r * CellSize;
                var x1 = x0 + w * CellSize;
                var z1 = z0 + d * CellSize;
                builder.AddQuadWorldUv((int)type,
                    new Vector3(x0, h, z0), new Vector3(x0, h, z1), new Vector3(x1, h, z1), new Vector3(x1, h, z0), color);
            }
        }

        bool Matches(int c, int r, float h, GroundType type, bool[] used)
        {
            var i = Index(c, r);
            return !used[i] && !_smooth[i] && _types[i] == type && Mathf.Abs(_heights[i] - h) < 0.001f;
        }

        bool RowMatches(int c, int r, int w, float h, GroundType type, bool[] used)
        {
            for (var cc = c; cc < c + w; cc++)
                if (!Matches(cc, r, h, type, used))
                    return false;
            return true;
        }

        void BuildSmoothTops(MeshBuilder builder, Color32 color)
        {
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Columns; c++)
            {
                if (!IsSmooth(c, r))
                    continue;

                var x0 = MinX + c * CellSize;
                var z0 = MinZ + r * CellSize;
                var x1 = x0 + CellSize;
                var z1 = z0 + CellSize;
                builder.AddQuadWorldUv((int)GetGround(c, r),
                    new Vector3(x0, CornerHeight(c, r, 0, 0), z0),
                    new Vector3(x0, CornerHeight(c, r, 0, 1), z1),
                    new Vector3(x1, CornerHeight(c, r, 1, 1), z1),
                    new Vector3(x1, CornerHeight(c, r, 1, 0), z0),
                    color);
            }
        }

        /// <summary>Боковины между соседними клетками там, где высоты на общем ребре различаются.</summary>
        void BuildSides(MeshBuilder builder, Color32 color)
        {
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Columns; c++)
            {
                var x0 = MinX + c * CellSize;
                var z0 = MinZ + r * CellSize;
                var x1 = x0 + CellSize;
                var z1 = z0 + CellSize;

                // Восточное ребро (общее с клеткой c + 1).
                {
                    var a0 = CornerHeight(c, r, 1, 0);
                    var a1 = CornerHeight(c, r, 1, 1);
                    float b0, b1;
                    if (InBounds(c + 1, r)) { b0 = CornerHeight(c + 1, r, 0, 0); b1 = CornerHeight(c + 1, r, 0, 1); }
                    else { b0 = b1 = BottomHeight; }
                    AddEdge(builder, c, r, c + 1, r, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), a0, a1, b0, b1, true, color);
                }

                // Северное ребро (общее с клеткой r + 1).
                {
                    var a0 = CornerHeight(c, r, 0, 1);
                    var a1 = CornerHeight(c, r, 1, 1);
                    float b0, b1;
                    if (InBounds(c, r + 1)) { b0 = CornerHeight(c, r + 1, 0, 0); b1 = CornerHeight(c, r + 1, 1, 0); }
                    else { b0 = b1 = BottomHeight; }
                    AddEdge(builder, c, r, c, r + 1, new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), a0, a1, b0, b1, false, color);
                }

                // Западный и южный края сетки.
                if (c == 0)
                {
                    var h0 = CornerHeight(c, r, 0, 0);
                    var h1 = CornerHeight(c, r, 0, 1);
                    AddEdge(builder, -1, r, c, r, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1),
                        BottomHeight, BottomHeight, h0, h1, true, color);
                }

                if (r == 0)
                {
                    var h0 = CornerHeight(c, r, 0, 0);
                    var h1 = CornerHeight(c, r, 1, 0);
                    AddEdge(builder, c, -1, c, r, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0),
                        BottomHeight, BottomHeight, h0, h1, false, color);
                }
            }
        }

        /// <summary>
        /// Ребро между клеткой A (запад/юг) и B (восток/север). e0, e1 — концы ребра (y игнорируется),
        /// a0/a1 и b0/b1 — высоты A и B на этих концах. Грань смотрит в сторону более низкой клетки.
        /// </summary>
        void AddEdge(MeshBuilder builder, int ac, int ar, int bc, int br, Vector3 e0, Vector3 e1,
            float a0, float a1, float b0, float b1, bool eastEdge, Color32 color)
        {
            if (Mathf.Abs(a0 - b0) < 0.001f && Mathf.Abs(a1 - b1) < 0.001f)
                return;

            var aHigher = a0 + a1 > b0 + b1;
            var hc = aHigher ? ac : bc;
            var hr = aHigher ? ar : br;
            var submesh = SideEarth;
            if (InBounds(hc, hr))
            {
                var ground = GetGround(hc, hr);
                if (ground == GroundType.Flagstone)
                    submesh = SideStone;
                else if (ground is GroundType.Rock or GroundType.Snow || _rockyCliff[Index(hc, hr)])
                    submesh = SideRock;
            }

            Vector3 P(Vector3 e, float y) => new(e.x, y, e.z);

            if (eastEdge)
            {
                // Ребро вдоль Z. A выше — грань смотрит на +X, иначе на −X.
                if (aHigher)
                    builder.AddQuadWorldUv(submesh, P(e0, b0), P(e0, a0), P(e1, a1), P(e1, b1), color);
                else
                    builder.AddQuadWorldUv(submesh, P(e1, a1), P(e1, b1), P(e0, b0), P(e0, a0), color);
            }
            else
            {
                // Ребро вдоль X. A выше — грань смотрит на +Z, иначе на −Z.
                if (aHigher)
                    builder.AddQuadWorldUv(submesh, P(e1, b1), P(e1, a1), P(e0, a0), P(e0, b0), color);
                else
                    builder.AddQuadWorldUv(submesh, P(e0, a0), P(e0, b0), P(e1, b1), P(e1, a1), color);
            }
        }
    }
}
