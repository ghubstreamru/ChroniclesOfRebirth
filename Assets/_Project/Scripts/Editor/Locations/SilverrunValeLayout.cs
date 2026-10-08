using System.Collections.Generic;
using ChroniclesOfRebirth.Core.Pixel;
using ChroniclesOfRebirth.Core.World;
using ChroniclesOfRebirth.Gameplay.World;
using UnityEngine;

namespace ChroniclesOfRebirth.Editor.Locations
{
    /// <summary>
    /// Планировка первой локации — Долина Серебрянки (Docs/Design/Locations/01_Awakening.md и 02_OldForest.md):
    /// Берег Пробуждения, Рассветное святилище и Мерцающий лес с сетью полян до выхода к Тихому Омуту.
    /// Ось X — восток (вниз по течению), ось Z — север (от камеры).
    /// </summary>
    public sealed class SilverrunValeLayout
    {
        // ---------- Размеры ----------

        /// <summary>Игровая зона: [0, PlayWidth) × [0, PlayDepth). Вокруг — декоративные поля.</summary>
        public const int PlayWidth = 96;
        public const int PlayDepth = 170;
        public const int MarginWest = 30, MarginEast = 30, MarginSouth = 14, MarginNorth = 30;

        public const int DeepLevel = -3, ShallowLevel = -1, ShoreLevel = 0, MeadowLevel = 1, PlateauLevel = 5;
        public const float WaterHeight = -0.15f;
        public const float BottomHeight = -2.5f;

        public static readonly Vector2 PortalCenter = new(41f, 25f);
        public static readonly Vector2 PlateauRadius = new(8.5f, 6.5f);
        static readonly Vector2 FlagstoneRadius = new(6.2f, 4.6f);
        public static readonly Vector3 HeroSpawn = new(20.5f, 0f, 12.6f);
        public static readonly Vector2 GiantOakPosition = new(48f, 65f);

        // ---------- Горы: от Дуба-Прадеда к перевалу и вниз, в поле у деревни ----------

        /// <summary>Линия начала подъёма — Дуб-Прадед.</summary>
        const float MountainStartZ = 58f;
        /// <summary>Перевал на основной тропе (Утёс Первого Взгляда) и его высота в юнитах.</summary>
        const float PassZ = 111f, PassHeight = 10f;
        const int PassLevel = 20;
        /// <summary>Подножие северного склона: дальше поле, в котором стоит деревня.</summary>
        const float FieldZ = 140f;
        /// <summary>Сколько добавляют хребты вдали от долин.</summary>
        const float PeakHeight = 13f;
        const float SnowLine = 17f, TreeLine = 11f;

        /// <summary>
        /// Долины, по которым идут тропы: горы поднимаются по мере удаления от них, поэтому тропы
        /// всегда лежат в понижениях, а пики — между долинами и по краям.
        /// </summary>
        static readonly Vector2[][] Valleys =
        {
            new Vector2[] { new(48f, 56f), new(52f, 74f), new(44f, 86f), new(53f, 98f), new(48f, PassZ), new(40f, 121f), new(55f, 130f), new(50f, 142f) },
            new Vector2[] { new(22f, 60f), new(17f, 72f), new(16f, 86f), new(28f, 98f), new(48f, PassZ) },
            new Vector2[] { new(48f, PassZ), new(62f, 104f), new(76f, 93f) },
            new Vector2[] { new(48f, 62f), new(60f, 66f), new(70f, 72f), new(62f, 58f) },
        };

        // ---------- Типы данных ----------

        public enum TreeKind { Orange, Red, Yellow, Conifer }

        public struct TreePlacement
        {
            public Vector2 Position;
            public TreeKind Kind;
            public float Scale;
            public bool Old;
            public bool Giant;
        }

        public struct GroundSprite
        {
            public Vector3 Position;
            public int Variant;
        }

        public struct MarkerPlacement
        {
            public WorldMarkerKind Kind;
            public string Id;
            public Vector3 Position;
            public float Radius;
            public string NameKey;
            public string Note;
        }

        /// <summary>Зона камеры: круг, в котором действует свой ракурс (направление камеры везде одно — фиксированное).</summary>
        public struct CameraZonePlacement
        {
            public string Id;
            public Vector2 Center;
            public float Radius;
            public float Pitch;
            public float Distance;
            public float FieldOfView;
            public int Priority;
        }

        public struct NamedPlace
        {
            public string Key;
            public string Ru;
            public string En;
        }

        sealed class Glade
        {
            public string Id;
            public string Ru;
            public string En;
            public Vector2 Center;
            public float Radius;
            public int Level;           // NaturalLevel — взять уровень окружающего рельефа в центре
            public bool Flatten = true; // false — только зона и маркеры, рельеф не трогаем (брод)
            public GroundType Ground;
        }

        sealed class Trail
        {
            public Vector2[] Points;
            public float Width;
            public bool FollowTerrain = true;
        }

        const int NaturalLevel = int.MinValue;

        // ---------- Поляны и тропы (граф из 02_OldForest.md) ----------

        static readonly Glade[] Glades =
        {
            new() { Id = "snarepath", Ru = "Тропа Силков", En = "Snarepath", Center = new(9f, 46f), Radius = 5f, Level = NaturalLevel, Ground = GroundType.GrassLeaves },
            new() { Id = "rutted_glade", Ru = "Колейная поляна", En = "Rutted Glade", Center = new(22f, 60f), Radius = 7f, Level = NaturalLevel, Ground = GroundType.Grass },
            new() { Id = "grandfather_oak", Ru = "Дуб-Прадед", En = "Grandfather Oak", Center = new(48f, 62f), Radius = 8.5f, Level = NaturalLevel, Ground = GroundType.GrassLeaves },
            new() { Id = "ribbon_hollow", Ru = "Ленточный овраг", En = "Ribbon Hollow", Center = new(70f, 72f), Radius = 6f, Level = 1, Ground = GroundType.ForestFloor },
            new() { Id = "sleeping_lights_grotto", Ru = "Грот Спящих Огней", En = "Grotto of Sleeping Lights", Center = new(62f, 58f), Radius = 3.8f, Level = 1, Ground = GroundType.ForestFloor },
            new() { Id = "smoulder_camp", Ru = "Тлеющий лагерь", En = "Smoulder Camp", Center = new(16f, 86f), Radius = 6f, Level = NaturalLevel, Ground = GroundType.Grass },
            new() { Id = "first_sight_bluff", Ru = "Утёс Первого Взгляда", En = "First Sight Bluff", Center = new(48f, PassZ), Radius = 7f, Level = PassLevel, Ground = GroundType.Grass },
            new() { Id = "stepstone_ford", Ru = "Каменный брод", En = "Stepstone Ford", Center = new(78f, 92f), Radius = 5f, Level = NaturalLevel, Flatten = false, Ground = GroundType.Pebbles },
        };

        /// <summary>
        /// Редколесья: большие открытые участки внутри леса с редкими деревьями — по ним можно бродить свободно.
        /// Перепады высот внутри сглажены до одной ступеньки.
        /// </summary>
        static readonly (string id, Vector2 center, float radius)[] Woodlands =
        {
            ("woodland_hunters", new Vector2(13f, 54f), 10f),   // между Тропой Силков и Колейной поляной
            ("woodland_entrance", new Vector2(27f, 32f), 10f),  // опушка: открывается прямо на луг
            // Севернее Дуба-Прадеда — горы, по ним и так можно бродить свободно.
        };

        static readonly Trail[] Trails =
        {
            // Основная тропа: святилище → Дуб-Прадед → серпантин к перевалу (Утёс) → спуск в поле к деревне.
            new() { Points = new Vector2[] { new(41.5f, 29.5f), new(42.5f, 36f), new(44f, 46f), new(48f, 62f) }, Width = 2.6f },
            new() { Points = new Vector2[] { new(48f, 62f), new(52f, 74f), new(44f, 86f), new(53f, 98f), new(48f, PassZ) }, Width = 2.6f },
            new() { Points = new Vector2[] { new(48f, PassZ), new(40f, 121f), new(55f, 130f), new(50f, 142f), new(55f, 156f) }, Width = 2.6f },
            // Охотничья тропа от переката к Тропе Силков и дальше.
            new() { Points = new Vector2[] { new(13f, 15f), new(8f, 28f), new(7f, 38f), new(9f, 46f) }, Width = 2f },
            new() { Points = new Vector2[] { new(9f, 46f), new(14f, 54f), new(22f, 60f) }, Width = 2f },
            new() { Points = new Vector2[] { new(22f, 60f), new(34f, 64f), new(48f, 62f) }, Width = 2.2f },
            new() { Points = new Vector2[] { new(22f, 60f), new(17f, 72f), new(16f, 86f) }, Width = 2f },
            new() { Points = new Vector2[] { new(16f, 86f), new(28f, 98f), new(48f, PassZ) }, Width = 2f },
            // Спуск в овраг и к гроту — по дну, не по рельефу.
            new() { Points = new Vector2[] { new(48f, 62f), new(60f, 66f), new(70f, 72f) }, Width = 2f },
            new() { Points = new Vector2[] { new(70f, 72f), new(66f, 64f), new(62f, 58f) }, Width = 1.8f, FollowTerrain = false },
            // От перевала вниз к броду, и речная тропа по ущелью Медвянки.
            new() { Points = new Vector2[] { new(48f, PassZ), new(62f, 104f), new(76f, 93f) }, Width = 2f },
            new() { Points = new Vector2[] { new(78f, 12f), new(84f, 28f), new(85f, 50f), new(80f, 72f), new(74f, 89f) }, Width = 2f },
        };

        /// <summary>
        /// Зоны камеры. Обычный ракурс — наклон ≈34°, дистанция ≈20, угол обзора 26°.
        /// При перекрытии побеждает больший приоритет (перевал внутри зоны склона).
        /// </summary>
        public static readonly CameraZonePlacement[] CameraZones =
        {
            // Серпантин: чуть выше обычного — видно, куда ведёт тропа и где обрывы.
            new() { Id = "serpentine", Center = new(48f, 90f), Radius = 21f, Pitch = 42f, Distance = 23f, FieldOfView = 26f, Priority = 15 },
            // Северный склон: выше и круче — склон за спиной не закрывает героя, виден путь вниз.
            new() { Id = "north_slope", Center = new(48f, 129f), Radius = 15f, Pitch = 52f, Distance = 22f, FieldOfView = 26f, Priority = 20 },
            // Перевал: дальше и положе, шире обзор — панорама на поле и деревню, «момент открытия».
            new() { Id = "first_sight_bluff", Center = new(48f, PassZ), Radius = 6.5f, Pitch = 24f, Distance = 30f, FieldOfView = 34f, Priority = 30 },
            // Дуб-Прадед: ниже и ближе — дуб в кадре целиком и кажется огромным.
            new() { Id = "grandfather_oak", Center = new(48f, 61f), Radius = 9f, Pitch = 20f, Distance = 18f, FieldOfView = 32f, Priority = 30 },
        };

        /// <summary>Ручей Медвянка: с севера через брод в Серебрянку.</summary>
        static readonly Vector2[] StreamPath =
        {
            new(92f, 150f), new(86f, 124f), new(78f, 92f), new(84f, 74f), new(90f, 52f), new(88f, 28f), new(87f, 4f),
        };

        public static readonly NamedPlace[] Names =
        {
            new() { Key = "loc.silverrun_vale", Ru = "Долина Серебрянки", En = "Silverrun Vale" },
            new() { Key = "loc.silverrun", Ru = "Серебрянка", En = "Silverrun" },
            new() { Key = "loc.honeybrook", Ru = "Медвянка", En = "Honeybrook" },
            new() { Key = "loc.glimmerwood", Ru = "Мерцающий лес", En = "Glimmerwood" },
            new() { Key = "loc.waking_shore", Ru = "Берег Пробуждения", En = "Waking Shore" },
            new() { Key = "loc.dawnstone_shrine", Ru = "Рассветное святилище", En = "Dawnstone Shrine" },
            new() { Key = "loc.chiming_rapids", Ru = "Звонкие пороги", En = "Chiming Rapids" },
            new() { Key = "loc.mistshallows", Ru = "Туманные отмели", En = "Mistshallows" },
            new() { Key = "loc.stillwater", Ru = "Тихий Омут", En = "Stillwater" },
            new() { Key = "loc.old_watch", Ru = "Старая Дозорная", En = "Old Watch" },
        };

        // ---------- Результат ----------

        public TerrainField Field { get; }
        public List<TreePlacement> Trees { get; } = new();
        public List<Vector3> Bushes { get; } = new();
        public List<Vector4> Rocks { get; } = new();
        public List<GroundSprite> GroundSprites { get; } = new();
        public List<MarkerPlacement> Markers { get; } = new();

        readonly bool[] _walkable;
        readonly bool[] _locked;
        readonly bool[] _trail;

        public SilverrunValeLayout()
        {
            Field = new TerrainField(-MarginWest, -MarginSouth,
                PlayWidth + MarginWest + MarginEast, PlayDepth + MarginSouth + MarginNorth, BottomHeight);
            _walkable = new bool[Field.Columns * Field.Rows];
            _locked = new bool[Field.Columns * Field.Rows];
            _trail = new bool[Field.Columns * Field.Rows];

            BuildBaseTerrain();
            BuildPlateau();
            BuildWoodlands();
            BuildGlades();
            BuildStairs();
            BuildTrails();
            BuildFord();
            BuildWalkable();

            PlaceShoreTrees();
            PlaceForest();
            PlaceBushes();
            PlaceRocks();
            PlaceGroundSprites();
            PlacePointsOfInterest();
        }

        public bool IsWalkable(int c, int r) => Field.InBounds(c, r) && _walkable[r * Field.Columns + c];

        public bool IsWalkableAt(float x, float z) => Field.TryCell(x, z, out var c, out var r) && IsWalkable(c, r);

        public static bool InPlayArea(float x, float z) => x >= 0f && z >= 0f && x < PlayWidth && z < PlayDepth;

        // ---------- Шум и геометрия ----------

        static float Noise(float x, float z, float scale, int seed)
        {
            var sum = 0f;
            var amp = 0.5f;
            var norm = 0f;
            for (var o = 0; o < 3; o++)
            {
                var f = (1 << o) / scale;
                sum += PixelNoise.Value(x * f, z * f, 0, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        public static float RiverCenter(float x) => 6.5f + 1.4f * Mathf.Sin(x * 0.11f + 0.5f);

        static float ForestEdge(float x, float z) => 28f + 2f * Mathf.Sin(x * 0.17f) + (Noise(x, z, 9f, 7) - 0.5f) * 4f;

        /// <summary>Южная граница поля у деревни (подножие гор).</summary>
        static float FieldStart(float x, float z) => FieldZ + 2f * Mathf.Sin(x * 0.13f) + (Noise(x, z, 6f, 9) - 0.5f) * 3f;

        /// <summary>Горы: свободно проходимая зона между лесом у Дуба-Прадеда и полем.</summary>
        static bool InMountainZone(float x, float z) =>
            z > MountainStartZ + 6f + (Noise(x, z, 7f, 121) - 0.5f) * 4f && z <= FieldStart(x, z);

        static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

        /// <summary>«Гребнистый» шум: острые гребни вместо округлых холмов.</summary>
        static float Ridged(float x, float z, float scale, int seed) => 1f - Mathf.Abs(Noise(x, z, scale, seed) * 2f - 1f);

        static float ValleyDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            var best = float.MaxValue;
            foreach (var valley in Valleys)
                best = Mathf.Min(best, DistanceToPolyline(p, valley, out _, out _));
            return best + (Noise(x, z, 9f, 117) - 0.5f) * 5f;
        }

        /// <summary>Высота дна долин: плавный подъём от дуба к перевалу и спуск к полю.</summary>
        static float ValleyFloor(float z) => z <= PassZ
            ? PassHeight * Smooth(MountainStartZ, PassZ, z)
            : Mathf.Lerp(PassHeight, MeadowLevel * TerrainField.LevelHeight, Smooth(PassZ, FieldZ, z));

        /// <summary>
        /// Непрерывная высота гор в юнитах: дно долин + хребты вдали от долин + скальные гребни.
        /// У ручья Медвянки горы опускаются к воде — получается ущелье.
        /// </summary>
        static float MountainHeight(float x, float z)
        {
            var d = ValleyDistance(x, z);
            var amp = PeakHeight * Smooth(MountainStartZ + 6f, PassZ - 8f, z) * (1f - Smooth(PassZ + 8f, FieldZ + 4f, z));
            // Вдали от долин — не плато, а гребни и вершины: «гребнистый» шум крупного масштаба.
            var crest = Ridged(x, z, 24f, 131);
            var ridge = amp * Smooth(4f, 26f, d) * (0.35f + 1.05f * crest * crest) * (0.8f + 0.4f * Noise(x, z, 16f, 101));
            var crag = ((Noise(x, z, 6f, 103) - 0.5f) * 2.5f + Ridged(x, z, 10f, 107) * 2.2f) *
                       Smooth(MountainStartZ, MountainStartZ + 25f, z) * (1f - Smooth(FieldZ - 14f, FieldZ, z)) *
                       (0.3f + 0.7f * Smooth(3f, 14f, d));
            var h = ValleyFloor(z) + ridge + crag;
            var floor = MeadowLevel * TerrainField.LevelHeight;
            return Mathf.Max(floor, Mathf.Lerp(floor, h, Smooth(5f, 16f, StreamDistance(x, z))));
        }

        static float StreamDistance(float x, float z) => DistanceToPolyline(new Vector2(x, z), StreamPath, out _, out _) +
                                                         (Noise(x, z, 5f, 11) - 0.5f) * 0.8f;

        static float DistanceToPolyline(Vector2 p, Vector2[] path, out int segment, out float t)
        {
            var best = float.MaxValue;
            segment = 0;
            t = 0f;
            for (var i = 0; i < path.Length - 1; i++)
            {
                var a = path[i];
                var ab = path[i + 1] - a;
                var st = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                var d = Vector2.Distance(p, a + ab * st);
                if (d >= best)
                    continue;
                best = d;
                segment = i;
                t = st;
            }
            return best;
        }

        bool InPlateau(float x, float z, float scale = 1f)
        {
            var wobble = 1f + (Noise(x, z, 4f, 13) - 0.5f) * 0.12f;
            var dx = (x - PortalCenter.x) / (PlateauRadius.x * scale * wobble);
            var dz = (z - PortalCenter.y) / (PlateauRadius.y * scale * wobble);
            return dx * dx + dz * dz <= 1f;
        }

        static bool InFlagstones(float x, float z)
        {
            var wobble = 1f + (Noise(x, z, 3f, 17) - 0.5f) * 0.25f;
            var dx = (x - PortalCenter.x) / (FlagstoneRadius.x * wobble);
            var dz = (z - PortalCenter.y) / (FlagstoneRadius.y * wobble);
            return dx * dx + dz * dz <= 1f;
        }

        static float GladeDistance(Glade glade, float x, float z) => BlobDistance(glade.Center, glade.Radius, StableHash(glade.Id), x, z);

        /// <summary>
        /// Нормированное расстояние до центра пятна неправильной формы (меньше 1 — внутри).
        /// Радиус меняется по углу (крупные выступы и заливы) плюс мелкая неровность края.
        /// </summary>
        static float BlobDistance(Vector2 center, float radius, int seed, float x, float z)
        {
            var offset = new Vector2(x, z) - center;
            var angle = Mathf.Atan2(offset.y, offset.x);
            var lobes = Noise(Mathf.Cos(angle) * 1.6f + 10f, Mathf.Sin(angle) * 1.6f + 10f, 1f, seed);
            var edge = Noise(x, z, 3f, seed + 7);
            var r = radius * (0.7f + lobes * 0.6f) * (0.9f + edge * 0.2f);
            return offset.magnitude / r;
        }

        /// <summary>Хеш строки, одинаковый между запусками (string.GetHashCode может меняться).</summary>
        static int StableHash(string s)
        {
            unchecked
            {
                var h = 17;
                foreach (var ch in s)
                    h = h * 31 + ch;
                return h & 0xffff;
            }
        }

        bool IsWater(int c, int r)
        {
            var g = Field.GetGround(c, r);
            return g is GroundType.DeepBed or GroundType.ShallowBed;
        }

        int CellIndex(int c, int r) => r * Field.Columns + c;

        // ---------- Рельеф ----------

        void BuildBaseTerrain()
        {
            // 1. Низины (река, ручей, луга, поле) — сразу; лес и горы — непрерывной высотой для второго прохода.
            var highland = new float[Field.Columns * Field.Rows];
            for (var r = 0; r < Field.Rows; r++)
            for (var c = 0; c < Field.Columns; c++)
            {
                var p = Field.CellCenter(c, r);
                var i = CellIndex(c, r);
                if (TryLowland(p.x, p.y, out var level, out var ground))
                {
                    Field.SetLevel(c, r, level, ground);
                    highland[i] = float.NaN;
                    continue;
                }

                var m = MountainBlend(p.y);
                var forest = ForestLevel(p.x, p.y) * TerrainField.LevelHeight;
                highland[i] = m <= 0f ? forest : Mathf.Lerp(forest, Mathf.Max(forest, MountainHeight(p.x, p.y)), m);
            }

            // 2. Лес и горы: террасы, шаг которых зависит от крутизны склона.
            for (var r = 0; r < Field.Rows; r++)
            for (var c = 0; c < Field.Columns; c++)
            {
                var h = highland[CellIndex(c, r)];
                if (float.IsNaN(h))
                    continue;
                var p = Field.CellCenter(c, r);
                var m = MountainBlend(p.y);
                if (m <= 0f)
                {
                    Field.SetLevel(c, r, ForestLevel(p.x, p.y), GroundType.ForestFloor);
                    continue;
                }

                var slope = Slope(highland, c, r, h);
                var step = MountainStep(slope);
                var level = Mathf.Max(MeadowLevel, Mathf.RoundToInt(h / TerrainField.LevelHeight / step) * step);
                Field.SetLevel(c, r, level, MountainGround(p.x, p.y, level * TerrainField.LevelHeight, step, m));
                Field.SetRockyCliff(c, r, m > 0.3f);
            }
        }

        /// <summary>Доля «горности»: 0 в лесу южнее дуба, 1 — в горах.</summary>
        static float MountainBlend(float z) => Smooth(MountainStartZ, MountainStartZ + 16f, z);

        /// <summary>Уровень лесного рельефа (как раньше: мягкие террасы 2–6).</summary>
        static int ForestLevel(float x, float z) => Mathf.Clamp(3 + Mathf.RoundToInt((Noise(x, z, 22f, 23) - 0.5f) * 6f), 2, 6);

        /// <summary>
        /// Шаг террас в уровнях по крутизне: пологий склон — ступени по 0,5 (просто идёшь), средний — по 1 юниту
        /// (запрыгнуть), крутой — по 2 юнита (скальная стена, только обойти).
        /// </summary>
        static int MountainStep(float slope) => slope < 0.55f ? 1 : slope < 1.1f ? 2 : 4;

        /// <summary>Крутизна по непрерывной высоте: разности через две клетки в каждую сторону.</summary>
        float Slope(float[] heights, int c, int r, float own)
        {
            float H(int cc, int rr)
            {
                if (!Field.InBounds(cc, rr))
                    return own;
                var v = heights[CellIndex(cc, rr)];
                return float.IsNaN(v) ? own : v;
            }

            const int k = 2;
            var span = 2 * k * TerrainField.CellSize;
            var dx = (H(c + k, r) - H(c - k, r)) / span;
            var dz = (H(c, r + k) - H(c, r - k)) / span;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static GroundType MountainGround(float x, float z, float height, int step, float m)
        {
            if (height >= SnowLine + (Noise(x, z, 8f, 109) - 0.5f) * 3f)
                return GroundType.Snow;

            var rocky = step >= 2 || (height > 8.5f && Noise(x, z, 5f, 111) > 0.5f) || Noise(x, z, 4f, 113) > 0.74f;
            if (rocky && m > 0.4f)
                return GroundType.Rock;

            // Ниже — хвоя под редкими елями, выше — горный луг.
            if (m < 0.6f || (height < 5f && Noise(x, z, 6f, 115) > 0.5f))
                return GroundType.ForestFloor;
            return GroundType.Grass;
        }

        bool TryLowland(float x, float z, out int level, out GroundType ground)
        {
            (level, ground) = (0, GroundType.Grass);
            var cell = LowlandCell(x, z);
            if (cell == null)
                return false;
            (level, ground) = cell.Value;
            return true;
        }

        /// <summary>Река, ручей, луга и поле; null — лес или горы (их считает второй проход).</summary>
        (int level, GroundType ground)? LowlandCell(float x, float z)
        {
            var rc = RiverCenter(x);
            var signed = z - rc + (Noise(x, z, 6f, 3) - 0.5f) * 0.9f;
            var d = Mathf.Abs(signed);

            if (d < 2.3f) return (DeepLevel, GroundType.DeepBed);
            if (d < 3.8f) return (ShallowLevel, GroundType.ShallowBed);
            if (signed < 0f) return d < 5f ? (ShoreLevel, GroundType.Pebbles) : (MeadowLevel, GroundType.Grass);

            // Долина ручья Медвянки.
            var ds = StreamDistance(x, z);
            if (ds < 1.5f) return (ShallowLevel, GroundType.ShallowBed);
            if (ds < 2.8f) return (ShoreLevel, GroundType.Pebbles);
            if (ds < 6.5f + Noise(x, z, 7f, 19) * 2f) return (MeadowLevel, GroundType.Grass);

            if (d < 6.8f) return (ShoreLevel, GroundType.Pebbles);

            if (z > FieldStart(x, z)) return (MeadowLevel, GroundType.Grass);

            if (z > ForestEdge(x, z))
                return null;

            // Луг между берегом и лесом с редкими пригорками.
            return Noise(x, z, 8f, 29) > 0.68f ? (2, GroundType.Grass) : (MeadowLevel, GroundType.Grass);
        }

        void BuildPlateau()
        {
            for (var r = 0; r < Field.Rows; r++)
            for (var c = 0; c < Field.Columns; c++)
            {
                var p = Field.CellCenter(c, r);
                if (!InPlateau(p.x, p.y))
                    continue;
                Field.SetLevel(c, r, PlateauLevel, InFlagstones(p.x, p.y) ? GroundType.Flagstone : GroundType.Grass);
                _locked[CellIndex(c, r)] = true;
            }
        }

        int GladeLevel(Glade glade)
        {
            if (glade.Level != NaturalLevel)
                return glade.Level;
            Field.TryCell(glade.Center.x, glade.Center.y, out var c, out var r);
            return Mathf.RoundToInt(Field.GetHeight(c, r) / TerrainField.LevelHeight);
        }

        static bool InWoodland(float x, float z)
        {
            foreach (var w in Woodlands)
                if (BlobDistance(w.center, w.radius, StableHash(w.id), x, z) < 1f)
                    return true;
            return false;
        }

        void BuildWoodlands()
        {
            foreach (var w in Woodlands)
            {
                Field.TryCell(w.center.x, w.center.y, out var cc, out var cr);
                var centerLevel = Mathf.RoundToInt(Field.GetHeight(cc, cr) / TerrainField.LevelHeight);
                var seed = StableHash(w.id);

                ForCellsAround(w.center, w.radius * 1.4f, (c, r, p) =>
                {
                    if (_locked[CellIndex(c, r)] || IsWater(c, r) || BlobDistance(w.center, w.radius, seed, p.x, p.y) > 1f)
                        return;
                    // Внутри редколесья перепад не больше одной ступеньки — везде можно пройти.
                    var natural = Mathf.RoundToInt(Field.GetHeight(c, r) / TerrainField.LevelHeight);
                    var level = natural > centerLevel ? centerLevel + 1 : centerLevel;
                    var ground = Noise(p.x, p.y, 5f, seed + 3) > 0.5f ? GroundType.GrassLeaves : GroundType.ForestFloor;
                    Field.SetLevel(c, r, level, ground);
                });
            }
        }

        void BuildGlades()
        {
            foreach (var glade in Glades)
            {
                if (!glade.Flatten)
                    continue;

                var level = GladeLevel(glade);
                ForCellsAround(glade.Center, glade.Radius * 1.4f, (c, r, p) =>
                {
                    if (_locked[CellIndex(c, r)] || GladeDistance(glade, p.x, p.y) > 1f)
                        return;
                    Field.SetLevel(c, r, level, glade.Ground);
                });
            }
        }

        void BuildStairs()
        {
            // Южная лестница святилища: луг (1) → 2 → 3 → 4 → плато (5). Рукотворная — прямые ступени.
            for (var x = 39f; x < 43f; x += TerrainField.CellSize)
            {
                SetStep(x, 16f, 17f, 2);
                SetStep(x, 17f, 18f, 3);
                SetStep(x, 18f, 19f, 4);
            }

            // Северный выход в лес — плавный пандус: его делает основная тропа, начинающаяся на плато.
        }

        void SetStep(float x, float z0, float z1, int level)
        {
            for (var z = z0; z < z1; z += TerrainField.CellSize)
            {
                if (!Field.TryCell(x + 0.01f, z + 0.01f, out var c, out var r))
                    continue;
                Field.SetLevel(c, r, level, GroundType.Flagstone);
                _locked[CellIndex(c, r)] = true;
            }
        }

        void BuildTrails()
        {
            foreach (var trail in Trails)
            {
                var samples = SampleTrail(trail);
                PaintTrail(trail, samples);
            }
        }

        /// <summary>
        /// Плавная кривая через опорные точки (Catmull-Rom) с лёгким петлянием — без прямых отрезков и углов.
        /// Петляние затухает к концам, чтобы тропа точно приходила в центр поляны.
        /// </summary>
        static List<Vector2> TrailCurve(Trail trail)
        {
            var cp = trail.Points;
            var raw = new List<Vector2>();
            for (var i = 0; i < cp.Length - 1; i++)
            {
                var p0 = cp[Mathf.Max(i - 1, 0)];
                var p1 = cp[i];
                var p2 = cp[i + 1];
                var p3 = cp[Mathf.Min(i + 2, cp.Length - 1)];
                var steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.5f));
                for (var s = 0; s < steps; s++)
                {
                    var t = s / (float)steps;
                    var t2 = t * t;
                    var t3 = t2 * t;
                    raw.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            raw.Add(cp[^1]);

            var result = new List<Vector2>(raw.Count);
            for (var i = 0; i < raw.Count; i++)
            {
                var prev = raw[Mathf.Max(i - 1, 0)];
                var next = raw[Mathf.Min(i + 1, raw.Count - 1)];
                var tangent = (next - prev).normalized;
                var normal = new Vector2(-tangent.y, tangent.x);
                var along = i / (float)(raw.Count - 1);
                var fade = Mathf.Sin(along * Mathf.PI);
                var meander = (Noise(raw[i].x, raw[i].y, 11f, 41) - 0.5f) * 5f * fade;
                result.Add(raw[i] + normal * meander);
            }
            return result;
        }

        List<(Vector2 point, float height)> SampleTrail(Trail trail)
        {
            var points = TrailCurve(trail);

            var heights = new float[points.Count];
            var start = Field.SampleHeight(points[0].x, points[0].y);
            var end = Field.SampleHeight(points[^1].x, points[^1].y);
            for (var i = 0; i < points.Count; i++)
            {
                heights[i] = trail.FollowTerrain
                    ? Field.SampleHeight(points[i].x, points[i].y)
                    : Mathf.Lerp(start, end, i / (points.Count - 1f));
            }

            // Сглаживание с закреплёнными концами и ограничение уклона (≈ 22°).
            for (var pass = 0; pass < 4; pass++)
            {
                var copy = (float[])heights.Clone();
                for (var i = 1; i < heights.Length - 1; i++)
                {
                    var sum = 0f;
                    var n = 0;
                    for (var k = -6; k <= 6; k++)
                    {
                        var j = Mathf.Clamp(i + k, 0, heights.Length - 1);
                        sum += copy[j];
                        n++;
                    }
                    heights[i] = sum / n;
                }
            }

            const float maxStep = 0.2f;
            for (var pass = 0; pass < 3; pass++)
            {
                for (var i = 1; i < heights.Length; i++)
                    heights[i] = Mathf.Clamp(heights[i], heights[i - 1] - maxStep, heights[i - 1] + maxStep);
                for (var i = heights.Length - 2; i >= 0; i--)
                    heights[i] = Mathf.Clamp(heights[i], heights[i + 1] - maxStep, heights[i + 1] + maxStep);
            }

            var result = new List<(Vector2, float)>(points.Count);
            for (var i = 0; i < points.Count; i++)
                result.Add((points[i], heights[i]));
            return result;
        }

        void PaintTrail(Trail trail, List<(Vector2 point, float height)> samples)
        {
            var min = trail.Points[0];
            var max = trail.Points[0];
            foreach (var p in trail.Points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            var reach = trail.Width + 4f; // запас на петляние кривой
            for (var z = min.y - reach; z <= max.y + reach; z += TerrainField.CellSize)
            for (var x = min.x - reach; x <= max.x + reach; x += TerrainField.CellSize)
            {
                if (!Field.TryCell(x, z, out var c, out var r))
                    continue;
                var center = Field.CellCenter(c, r);
                var nearest = NearestSample(samples, center, out var distance);
                var half = trail.Width * 0.5f + (Noise(center.x, center.y, 3f, 37) - 0.5f) * 1f;
                if (distance > half)
                    continue;

                var i = CellIndex(c, r);
                _walkable[i] = true;
                _trail[i] = true;
                if (_locked[i] || IsWater(c, r))
                    continue;

                if (InFlattenedGlade(center.x, center.y))
                {
                    Field.SetGround(c, r, GroundType.Dirt);
                    continue;
                }

                Field.SetSmooth(c, r, samples[nearest].height, GroundType.Dirt);
            }
        }

        static int NearestSample(List<(Vector2 point, float height)> samples, Vector2 p, out float distance)
        {
            var best = 0;
            distance = float.MaxValue;
            for (var i = 0; i < samples.Count; i++)
            {
                var d = (samples[i].point - p).sqrMagnitude;
                if (d >= distance)
                    continue;
                distance = d;
                best = i;
            }
            distance = Mathf.Sqrt(distance);
            return best;
        }

        bool InFlattenedGlade(float x, float z)
        {
            foreach (var glade in Glades)
                if (glade.Flatten && GladeDistance(glade, x, z) < 0.85f)
                    return true;
            return false;
        }

        /// <summary>Брод: глубокий участок ручья, перейти его можно будет позже.</summary>
        void BuildFord()
        {
            var ford = System.Array.Find(Glades, g => g.Id == "stepstone_ford");
            ForCellsAround(ford.Center, 3f, (c, r, p) =>
            {
                if (Field.GetGround(c, r) == GroundType.ShallowBed && Vector2.Distance(p, ford.Center) < 2.6f)
                    Field.SetLevel(c, r, DeepLevel, GroundType.DeepBed);
            });
        }

        void BuildWalkable()
        {
            for (var r = 0; r < Field.Rows; r++)
            for (var c = 0; c < Field.Columns; c++)
            {
                var i = CellIndex(c, r);
                var p = Field.CellCenter(c, r);
                if (!InPlayArea(p.x, p.y) || Field.GetGround(c, r) == GroundType.DeepBed)
                {
                    _walkable[i] = false;
                    continue;
                }

                if (_walkable[i] || _locked[i])
                {
                    _walkable[i] = true;
                    continue;
                }

                var rc = RiverCenter(p.x);
                var southZone = p.y < ForestEdge(p.x, p.y);
                var streamValley = p.y > rc + 3.8f && StreamDistance(p.x, p.y) < 6.5f + Noise(p.x, p.y, 7f, 19) * 2f;
                var field = p.y > FieldStart(p.x, p.y);
                var inGlade = false;
                foreach (var glade in Glades)
                    inGlade |= GladeDistance(glade, p.x, p.y) < 1f;

                _walkable[i] = southZone || streamValley || field || InMountainZone(p.x, p.y) || inGlade || InWoodland(p.x, p.y);
            }
        }

        /// <summary>Клетка, куда герой не может зайти: глубокая вода или вне игровой зоны. Лес проходим — свобода.</summary>
        bool IsImpassable(int c, int r)
        {
            if (!Field.InBounds(c, r))
                return true;
            var p = Field.CellCenter(c, r);
            return !InPlayArea(p.x, p.y) || Field.GetGround(c, r) == GroundType.DeepBed;
        }

        /// <summary>Невидимые стены у глубокой воды (полосы клеток, объединённые по строкам).</summary>
        public List<(Vector3 center, Vector3 size)> BuildBlockerBoxes()
        {
            var boxes = new List<(Vector3, Vector3)>();
            for (var r = 0; r < Field.Rows; r++)
            {
                var runStart = -1;
                for (var c = 0; c <= Field.Columns; c++)
                {
                    var blocker = c < Field.Columns && IsImpassable(c, r) &&
                                  (!IsImpassable(c + 1, r) || !IsImpassable(c - 1, r) || !IsImpassable(c, r + 1) || !IsImpassable(c, r - 1));
                    if (blocker && runStart < 0)
                        runStart = c;
                    if (blocker || runStart < 0)
                        continue;

                    var start = Field.CellCenter(runStart, r);
                    var end = Field.CellCenter(c - 1, r);
                    var y = Field.GetHeight(runStart, r);
                    boxes.Add((new Vector3((start.x + end.x) * 0.5f, y + 1.5f, start.y),
                        new Vector3(end.x - start.x + TerrainField.CellSize, 7f, TerrainField.CellSize)));
                    runStart = -1;
                }
            }
            return boxes;
        }

        // ---------- Растительность ----------

        void PlaceShoreTrees()
        {
            // Старое дерево с корнями-«пальцами» над водой у места пробуждения.
            Trees.Add(new TreePlacement { Position = new Vector2(17.2f, 14.6f), Kind = TreeKind.Orange, Scale = 1.25f, Old = true });

            AddTree(5f, 1.2f, TreeKind.Red, 1.1f);
            AddTree(29f, 0.8f, TreeKind.Yellow, 1.15f);
            AddTree(46f, 1.6f, TreeKind.Orange, 1.1f);
            AddTree(53f, 0.6f, TreeKind.Conifer, 1.2f);
            AddTree(66f, 1.4f, TreeKind.Red, 1.05f);
            AddTree(26.5f, 20.5f, TreeKind.Red, 1.05f);
            AddTree(31.5f, 16.8f, TreeKind.Orange, 0.8f);
            AddTree(52.5f, 17.5f, TreeKind.Red, 1f);
            AddTree(62f, 20f, TreeKind.Yellow, 1f);
            AddTree(3f, 18f, TreeKind.Red, 0.9f);
            AddTree(47.5f, 28.5f, TreeKind.Yellow, 0.75f); // проросло среди руин
        }

        void PlaceForest()
        {
            var i = 0;
            for (var z = Field.MinZ + 1f; z < Field.MaxZ; z += 3.6f)
            for (var x = Field.MinX + 1f; x < Field.MaxX; x += 3.6f)
            {
                i++;
                var p = new Vector2(
                    x + (PixelNoise.Hash01(i, 0, 41) - 0.5f) * 2.6f,
                    z + (PixelNoise.Hash01(i, 1, 41) - 0.5f) * 2.6f);
                if (!Field.TryCell(p.x, p.y, out var c, out var r))
                    continue;

                var ground = Field.GetGround(c, r);
                if (ground is GroundType.DeepBed or GroundType.ShallowBed or GroundType.Pebbles or GroundType.Flagstone or GroundType.Dirt
                    or GroundType.Rock or GroundType.Snow)
                    continue;
                if (Vector2.Distance(p, GiantOakPosition) < 9f)
                    continue; // под кроной Дуба-Прадеда

                var inPlay = InPlayArea(p.x, p.y);
                var edge = ForestEdge(p.x, p.y);
                var forest = p.y > edge - 1f;
                var field = p.y > FieldStart(p.x, p.y);
                var roll = PixelNoise.Hash01(i, 5, 41);
                var scale = 0.95f + PixelNoise.Hash01(i, 3, 41) * 0.4f;

                if (InMountainZone(p.x, p.y))
                {
                    // Горы: редкие ели, всё реже к границе леса; на тропах и полянах — нет.
                    var chance = 0.3f * (1f - Smooth(TreeLine - 4f, TreeLine, Field.GetHeight(c, r)));
                    if (roll < chance && !NearTrail(p, 2.5f) && !InAnyGlade(p, 1.1f))
                        AddTree(p.x, p.y, TreeKind.Conifer, 0.75f + PixelNoise.Hash01(i, 3, 41) * 0.4f);
                    continue;
                }

                if (inPlay && InWoodland(p.x, p.y))
                {
                    // Редколесье: редкие деревья, в стороне от троп и полян.
                    if (roll < 0.14f && !NearTrail(p, 3.5f) && !InAnyGlade(p, 1.25f))
                        AddTree(p.x, p.y, RandomKind(i, 41, p, 0.2f), scale);
                    continue;
                }

                if (inPlay && !forest && !field)
                {
                    // Опушка: редкие деревья на лугу у кромки леса.
                    if (p.y > edge - 6f && roll < 0.2f && !NearTrail(p, 3.5f) && !InPlateau(p.x, p.y, 1.25f))
                        AddTree(p.x, p.y, RandomKind(i, 41, p, 0.1f), scale);
                    continue;
                }

                if (NearWalkable(p, 1.4f))
                    continue;
                if (inPlay && field)
                    continue;
                // За пределами зоны: редкие деревья на лугах, плотный лес в лесу.
                if (!inPlay && (!forest || field) && roll > 0.35f)
                    continue;

                AddTree(p.x, p.y, RandomKind(i, 41, p, 0.15f), scale);
            }

            // Подсад елей между узлами сетки: ели уже лиственных крон, и без него хвойная чаща выглядит редкой.
            var j = 0;
            for (var z = Field.MinZ + 2.8f; z < Field.MaxZ; z += 3.6f)
            for (var x = Field.MinX + 2.8f; x < Field.MaxX; x += 3.6f)
            {
                j++;
                var p = new Vector2(
                    x + (PixelNoise.Hash01(j, 0, 47) - 0.5f) * 1.6f,
                    z + (PixelNoise.Hash01(j, 1, 47) - 0.5f) * 1.6f);
                if (!Field.TryCell(p.x, p.y, out var c, out var r))
                    continue;
                if (Field.GetGround(c, r) is GroundType.DeepBed or GroundType.ShallowBed or GroundType.Pebbles or GroundType.Flagstone or GroundType.Dirt)
                    continue;
                if (p.y < ForestEdge(p.x, p.y) + 1f || p.y > FieldStart(p.x, p.y) || InMountainZone(p.x, p.y) ||
                    NearWalkable(p, 1.4f) || Vector2.Distance(p, GiantOakPosition) < 9f)
                    continue;
                if (PixelNoise.Hash01(j, 5, 47) > ConiferShare(p.y, 0f) - 0.1f)
                    continue;
                AddTree(p.x, p.y, TreeKind.Conifer, 0.85f + PixelNoise.Hash01(j, 3, 47) * 0.35f);
            }
        }

        /// <summary>Есть ли клетка тропы в радиусе (проверка по 9 точкам).</summary>
        bool NearTrail(Vector2 p, float radius)
        {
            for (var k = 0; k < 9; k++)
            {
                var offset = k == 0 ? Vector2.zero : new Vector2(Mathf.Cos(k * 0.785f), Mathf.Sin(k * 0.785f)) * radius;
                if (Field.TryCell(p.x + offset.x, p.y + offset.y, out var c, out var r) && _trail[CellIndex(c, r)])
                    return true;
            }
            return false;
        }

        static bool InAnyGlade(Vector2 p, float scale)
        {
            foreach (var glade in Glades)
                if (GladeDistance(glade, p.x, p.y) < scale)
                    return true;
            return false;
        }

        bool NearWalkable(Vector2 p, float radius)
        {
            for (var k = 0; k < 9; k++)
            {
                var offset = k == 0 ? Vector2.zero : new Vector2(Mathf.Cos(k * 0.785f), Mathf.Sin(k * 0.785f)) * radius;
                if (IsWalkableAt(p.x + offset.x, p.y + offset.y))
                    return true;
            }
            return false;
        }

        void AddTree(float x, float z, TreeKind kind, float scale) =>
            Trees.Add(new TreePlacement { Position = new Vector2(x, z), Kind = kind, Scale = scale });

        /// <summary>Переход к хвойному лесу: от опушки до Дуба-Прадеда лиственные сменяются елями.</summary>
        const float ConiferStartZ = 38f, ConiferFullZ = 62f, ConiferMaxShare = 0.9f;

        static float ConiferShare(float z, float southShare) =>
            Mathf.Lerp(southShare, ConiferMaxShare, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ConiferStartZ, ConiferFullZ, z)));

        static TreeKind RandomKind(int i, int seed, Vector2 p, float southShare) => RandomKind(i, seed, ConiferShare(p.y, southShare));

        static TreeKind RandomKind(int i, int seed, float coniferShare)
        {
            var roll = PixelNoise.Hash01(i, 2, seed);
            if (roll < coniferShare)
                return TreeKind.Conifer;
            var t = (roll - coniferShare) / (1f - coniferShare);
            return t < 0.4f ? TreeKind.Orange : t < 0.75f ? TreeKind.Red : TreeKind.Yellow;
        }

        /// <summary>Кусты вдоль края троп и полян — живые стены, которые задают проход.</summary>
        void PlaceBushes()
        {
            var i = 0;
            for (var r = 0; r < Field.Rows; r += 2)
            for (var c = 0; c < Field.Columns; c += 2)
            {
                i++;
                if (IsWalkable(c, r) || PixelNoise.Hash01(c, r, 43) > 0.2f)
                    continue;
                if (!(IsWalkable(c + 3, r) || IsWalkable(c - 3, r) || IsWalkable(c, r + 3) || IsWalkable(c, r - 3)))
                    continue;

                var p = Field.CellCenter(c, r);
                var ground = Field.GetGround(c, r);
                if (ground is GroundType.DeepBed or GroundType.ShallowBed or GroundType.Pebbles or GroundType.Flagstone)
                    continue;
                // На лугу у берега, в горах и в поле кусты не нужны — там открытое пространство.
                if (p.y < ForestEdge(p.x, p.y) - 2f || InMountainZone(p.x, p.y) || p.y > FieldStart(p.x, p.y))
                    continue;
                Bushes.Add(new Vector3(p.x, Field.GetHeight(c, r), p.y));
            }
        }

        void PlaceRocks()
        {
            // Камни у воды Серебрянки.
            for (var i = 0; i < 90; i++)
            {
                var x = -10f + PixelNoise.Hash01(i, 0, 53) * (PlayWidth + 20f);
                var side = PixelNoise.Hash01(i, 1, 53) < 0.6f ? 1f : -1f;
                var z = RiverCenter(x) + side * (2.6f + PixelNoise.Hash01(i, 2, 53) * 3.6f);
                if (Vector2.Distance(new Vector2(x, z), new Vector2(HeroSpawn.x, HeroSpawn.z)) < 2f)
                    continue;
                AddRock(x, z, 0.25f + PixelNoise.Hash01(i, 3, 53) * 0.5f);
            }

            // Валуны на Звонких порогах.
            for (var i = 0; i < 16; i++)
            {
                var x = -6f + PixelNoise.Hash01(i, 0, 59) * 14f;
                AddRock(x, RiverCenter(x) + (PixelNoise.Hash01(i, 1, 59) - 0.5f) * 6f, 0.7f + PixelNoise.Hash01(i, 2, 59) * 0.6f);
            }

            // Камни в ручье Медвянке.
            for (var i = 0; i < 50; i++)
            {
                var t = PixelNoise.Hash01(i, 0, 61);
                var seg = Mathf.Min(StreamPath.Length - 2, Mathf.FloorToInt(t * (StreamPath.Length - 1)));
                var p = Vector2.Lerp(StreamPath[seg], StreamPath[seg + 1], t * (StreamPath.Length - 1) - seg);
                p += new Vector2(PixelNoise.Hash01(i, 1, 61) - 0.5f, PixelNoise.Hash01(i, 2, 61) - 0.5f) * 4f;
                AddRock(p.x, p.y, 0.3f + PixelNoise.Hash01(i, 3, 61) * 0.5f);
            }

            // Замшелые валуны в лесу и у подножий обрывов.
            for (var i = 0; i < 160; i++)
            {
                var x = Field.MinX + PixelNoise.Hash01(i, 0, 67) * (Field.MaxX - Field.MinX);
                var z = 26f + PixelNoise.Hash01(i, 1, 67) * (MountainStartZ + 8f - 26f);
                if (NearWalkable(new Vector2(x, z), 1f) && PixelNoise.Hash01(i, 2, 67) > 0.25f)
                    continue;
                AddRock(x, z, 0.5f + PixelNoise.Hash01(i, 3, 67) * 0.8f);
            }

            // Горы: валуны и обломки скал повсюду, кроме троп и снега.
            for (var i = 0; i < 520; i++)
            {
                var x = Field.MinX + PixelNoise.Hash01(i, 0, 127) * (Field.MaxX - Field.MinX);
                var z = MountainStartZ + PixelNoise.Hash01(i, 1, 127) * (FieldZ - MountainStartZ);
                if (!InMountainZone(x, z) || !Field.TryCell(x, z, out var c, out var r))
                    continue;
                if (Field.GetGround(c, r) is GroundType.Snow or GroundType.Dirt or GroundType.ShallowBed or GroundType.DeepBed)
                    continue;
                if (NearTrail(new Vector2(x, z), 1.8f) || Vector2.Distance(new Vector2(x, z), GiantOakPosition) < 7f)
                    continue;
                AddRock(x, z, 0.5f + PixelNoise.Hash01(i, 3, 127) * 1.3f);
            }
        }

        void AddRock(float x, float z, float scale)
        {
            if (!Field.TryCell(x, z, out _, out _))
                return;
            // Центр чуть ниже земли: плоское основание камня уходит в грунт, камень не «стоит» на земле.
            Rocks.Add(new Vector4(x, Field.SampleHeight(x, z) - scale * 0.03f, z, scale));
        }

        void PlaceGroundSprites()
        {
            for (var z = Field.MinZ + 0.5f; z < Field.MaxZ; z += 1f)
            for (var x = Field.MinX + 0.5f; x < Field.MaxX; x += 1f)
            {
                if (!Field.TryCell(x, z, out var c, out var r))
                    continue;
                var ground = Field.GetGround(c, r);
                var nearRuins = Vector2.Distance(new Vector2(x, z), PortalCenter) < 11f;
                var ix = Mathf.FloorToInt(x * 4f);
                var iz = Mathf.FloorToInt(z * 4f);

                for (var k = 0; k < 2; k++)
                {
                    var roll = PixelNoise.Hash01(ix * 7 + k, iz, 67);
                    var px = x - 0.35f + PixelNoise.Hash01(ix, iz * 3 + k, 71) * 0.7f;
                    var pz = z - 0.35f + PixelNoise.Hash01(ix * 5 + k, iz, 73) * 0.7f;
                    var position = new Vector3(px, Field.SampleHeight(px, pz), pz);

                    switch (ground)
                    {
                        case GroundType.Grass:
                        case GroundType.GrassLeaves:
                            // Густой сплошной покров; цветы — редкая россыпь, а не букеты.
                            if (nearRuins && roll < 0.1f)
                                AddSprite(position, FoliageSprites.Flowers);
                            else if (roll < 0.72f)
                                AddSprite(position, PixelNoise.Hash01(ix, iz, 79 + k) < 0.3f ? FoliageSprites.TuftDry : FoliageSprites.TuftGreen);
                            else if (roll > 0.985f)
                                AddSprite(position, FoliageSprites.Flowers);
                            break;
                        case GroundType.ForestFloor:
                            if (roll < 0.3f)
                                AddSprite(position, FoliageSprites.Fern);
                            else if (roll < 0.5f)
                                AddSprite(position, PixelNoise.Hash01(ix, iz, 83 + k) < 0.5f ? FoliageSprites.TuftDry : FoliageSprites.TuftGreen);
                            break;
                        case GroundType.Flagstone:
                            if (Field.GetHeight(c, r) >= PlateauLevel * TerrainField.LevelHeight && roll < 0.08f &&
                                Vector2.Distance(new Vector2(px, pz), PortalCenter) > 5f)
                                AddSprite(position, FoliageSprites.Flowers);
                            break;
                        case GroundType.Pebbles:
                            if (roll < 0.04f)
                                AddSprite(position, FoliageSprites.TuftDry);
                            break;
                    }
                }
            }
        }

        void AddSprite(Vector3 position, int variant) => GroundSprites.Add(new GroundSprite { Position = position, Variant = variant });

        // ---------- Точки интереса и маркеры ----------

        Vector3 OnGround(Vector2 p) => new(p.x, Field.SampleHeight(p.x, p.y), p.y);

        void Marker(WorldMarkerKind kind, string id, Vector2 p, float radius, string note, string nameKey = null) =>
            Markers.Add(new MarkerPlacement { Kind = kind, Id = id, Position = OnGround(p) + Vector3.up * 0.5f, Radius = radius, NameKey = nameKey, Note = note });

        void PlacePointsOfInterest()
        {
            // Берег и святилище.
            Marker(WorldMarkerKind.Area, "waking_shore", new Vector2(20f, 12f), 7f, "Место пробуждения героя", "loc.waking_shore");
            Marker(WorldMarkerKind.Area, "dawnstone_shrine", PortalCenter, 8f, "Руины святилища с портальным кругом", "loc.dawnstone_shrine");
            Marker(WorldMarkerKind.Area, "chiming_rapids", new Vector2(4f, 9f), 6f, "Перекат выше по течению", "loc.chiming_rapids");
            Marker(WorldMarkerKind.Area, "mistshallows", new Vector2(64f, 9f), 8f, "Туманный берег ниже по течению", "loc.mistshallows");
            Marker(WorldMarkerKind.Quest, "clue_portal_circle", PortalCenter, 3f, "Портальный круг ещё светится");
            Marker(WorldMarkerKind.Quest, "clue_symbol_stone", PortalCenter + new Vector2(-4.5f, 1.5f), 1f, "Знак, совпадающий со знаком на одежде героя");
            Marker(WorldMarkerKind.Quest, "clue_inscriptions", PortalCenter + new Vector2(3.5f, 3.5f), 1f, "Обрывки надписей на плитах");
            Marker(WorldMarkerKind.Quest, "clue_broken_amulet", PortalCenter + new Vector2(2.5f, -3f), 1f, "Сломанный амулет среди обломков");
            Marker(WorldMarkerKind.Quest, "clue_footprints", PortalCenter + new Vector2(-2f, -4.5f), 2f, "Следы множества ног, а людей нет");

            // Поляны: зоны с названиями.
            foreach (var glade in Glades)
                Marker(WorldMarkerKind.Area, glade.Id, glade.Center, glade.Radius, glade.Ru, "loc." + glade.Id);

            // Якоря под будущие объекты окружения, лут, улики и активности.
            // Сами объекты (навес, повозка, палатка и т. д.) добавляются по одному на этапе окружения.
            var g = Center("snarepath");
            Marker(WorldMarkerKind.Loot, "snarepath_cache", g + new Vector2(-2f, 1f), 1f, "Тайник охотника — награда за тупик");
            Marker(WorldMarkerKind.Quest, "clue_hunter_shelter", g + new Vector2(1.5f, 1.8f), 1.5f, "Место под охотничий навес");

            g = Center("rutted_glade");
            Marker(WorldMarkerKind.Quest, "clue_broken_wagon", g + new Vector2(0.5f, 0.5f), 2f, "Место под разбитую повозку");
            Marker(WorldMarkerKind.Loot, "wagon_remains", g + new Vector2(-2.6f, -0.5f), 1f, "Остатки груза");

            // Дуб-Прадед — часть планировки: главный ориентир, видимый издалека.
            g = Center("grandfather_oak");
            Trees.Add(new TreePlacement { Position = GiantOakPosition, Kind = TreeKind.Orange, Scale = 1f, Old = true, Giant = true });
            Marker(WorldMarkerKind.Landmark, "grandfather_oak_tree", GiantOakPosition, 3f, "Видно издалека над кронами", "loc.grandfather_oak");
            Marker(WorldMarkerKind.Quest, "clue_carved_marks", g + new Vector2(1.2f, 1.8f), 1f, "Вырезанные метки на коре");
            Marker(WorldMarkerKind.Activity, "oak_rest", g + new Vector2(-3f, -2f), 1.5f, "Место отдыха под дубом");

            g = Center("ribbon_hollow");
            Marker(WorldMarkerKind.Quest, "clue_ribbons", g, 5f, "Место под выцветшие ленты на ветках");
            Marker(WorldMarkerKind.Loot, "hollow_cache", g + new Vector2(2f, 2.5f), 1f, "Награда за спуск в овраг");

            g = Center("sleeping_lights_grotto");
            Marker(WorldMarkerKind.Blocked, "grotto_entrance", g + new Vector2(0f, -2.8f), 1.5f, "Вход в грот: нужен ключ из другой локации");

            g = Center("smoulder_camp");
            Marker(WorldMarkerKind.Quest, "clue_warm_embers", g + new Vector2(-0.6f, -0.6f), 1.5f, "Место под кострище");
            Marker(WorldMarkerKind.Loot, "camp_supplies", g + new Vector2(3.6f, -1.5f), 1f, "Припасы в лагере");

            g = Center("first_sight_bluff");
            Marker(WorldMarkerKind.Landmark, "bluff_viewpoint", g + new Vector2(0f, 4f), 2f, "Перевал: отсюда впервые видна деревня в поле; место для камеры-панорамы", "loc.first_sight_bluff");
            Marker(WorldMarkerKind.Area, "stillwater", new Vector2(58f, 156f), 12f, "Поле с деревней Тихий Омут (строится следующим этапом)", "loc.stillwater");

            g = Center("stepstone_ford");
            Marker(WorldMarkerKind.Blocked, "ford_crossing", g, 2.5f, "Брод: перейти можно будет позже");
            Marker(WorldMarkerKind.Activity, "ford_fishing", g + new Vector2(-3.5f, 1.5f), 1.5f, "Место для рыбалки");
        }

        Vector2 Center(string id) => System.Array.Find(Glades, g => g.Id == id).Center;

        /// <summary>Поляны для карты и отчётов: id, русское название, центр, радиус.</summary>
        public static IEnumerable<(string id, string ru, Vector2 center, float radius)> GladeInfos()
        {
            foreach (var g in Glades)
                yield return (g.Id, g.Ru, g.Center, g.Radius);
        }

        public static IEnumerable<NamedPlace> AllNames()
        {
            foreach (var n in Names)
                yield return n;
            foreach (var g in Glades)
                yield return new NamedPlace { Key = "loc." + g.Id, Ru = g.Ru, En = g.En };
        }

        void ForCellsAround(Vector2 center, float radius, System.Action<int, int, Vector2> action)
        {
            for (var z = center.y - radius; z <= center.y + radius; z += TerrainField.CellSize)
            for (var x = center.x - radius; x <= center.x + radius; x += TerrainField.CellSize)
            {
                if (!Field.TryCell(x, z, out var c, out var r))
                    continue;
                action(c, r, Field.CellCenter(c, r));
            }
        }
    }
}
