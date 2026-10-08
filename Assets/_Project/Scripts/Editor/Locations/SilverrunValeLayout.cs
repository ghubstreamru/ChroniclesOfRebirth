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

        // ---------- Высоты (юниты) ----------

        public const float WaterHeight = -0.15f;
        public const float BottomHeight = -3f;
        /// <summary>Ниже — глубокая вода (не пройти), выше до уровня воды — мелководье.</summary>
        const float DeepWaterHeight = -0.8f;
        /// <summary>Типичная высота луга и поля.</summary>
        const float MeadowHeight = 0.75f;
        /// <summary>Вершина холма со святилищем.</summary>
        public const float PlateauTop = 3.2f;
        /// <summary>Дно оврага у Ленточного оврага и грота.</summary>
        const float RavineFloor = 0.9f;
        /// <summary>Склон круче — герой по нему не поднимется (как slopeLimit героя), лес на нём не растёт.</summary>
        public const float WalkableSlope = 44f;

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

        /// <summary>Ленточный овраг: русло сырого оврага от Дуба-Прадеда на восток и его отрог к Гроту Спящих Огней.</summary>
        static readonly Vector2[][] Ravines =
        {
            new Vector2[] { new(55.5f, 64.6f), new(60f, 66f), new(70f, 72f) },
            new Vector2[] { new(70f, 72f), new(66f, 64f), new(62f, 58.6f) },
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
            public bool Flatten = true; // false — только зона и маркеры, рельеф не трогаем (овраг, брод)
            public GroundType Ground;
        }

        sealed class Trail
        {
            public Vector2[] Points;
            public float Width;
            public bool FollowTerrain = true;
        }

        // ---------- Поляны и тропы (граф из 02_OldForest.md) ----------

        static readonly Glade[] Glades =
        {
            new() { Id = "snarepath", Ru = "Тропа Силков", En = "Snarepath", Center = new(9f, 46f), Radius = 5f, Ground = GroundType.GrassLeaves },
            new() { Id = "rutted_glade", Ru = "Колейная поляна", En = "Rutted Glade", Center = new(22f, 60f), Radius = 7f, Ground = GroundType.Grass },
            new() { Id = "grandfather_oak", Ru = "Дуб-Прадед", En = "Grandfather Oak", Center = new(48f, 62f), Radius = 8.5f, Ground = GroundType.GrassLeaves },
            new() { Id = "ribbon_hollow", Ru = "Ленточный овраг", En = "Ribbon Hollow", Center = new(70f, 72f), Radius = 6f, Flatten = false, Ground = GroundType.ForestFloor },
            new() { Id = "sleeping_lights_grotto", Ru = "Грот Спящих Огней", En = "Grotto of Sleeping Lights", Center = new(62f, 58f), Radius = 3.8f, Flatten = false, Ground = GroundType.ForestFloor },
            new() { Id = "smoulder_camp", Ru = "Тлеющий лагерь", En = "Smoulder Camp", Center = new(16f, 86f), Radius = 6f, Ground = GroundType.Grass },
            new() { Id = "first_sight_bluff", Ru = "Утёс Первого Взгляда", En = "First Sight Bluff", Center = new(48f, PassZ), Radius = 7f, Ground = GroundType.Grass },
            new() { Id = "stepstone_ford", Ru = "Каменный брод", En = "Stepstone Ford", Center = new(78f, 92f), Radius = 5f, Flatten = false, Ground = GroundType.Pebbles },
        };

        /// <summary>
        /// Редколесья: большие открытые участки внутри леса с редкими деревьями — по ним можно бродить свободно.
        /// Холмы внутри вдвое положе, чем в лесу вокруг.
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
            new() { Points = new Vector2[] { new(48f, PassZ), new(62f, 104f), new(74.5f, 94f) }, Width = 2f },
            new() { Points = new Vector2[] { new(78f, 12f), new(82.5f, 28f), new(83f, 50f), new(79f, 72f), new(74f, 89f) }, Width = 2f },
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
        /// <summary>Мелкие камни (галька у воды, камешки на тропах): xyz — позиция, w — размер.</summary>
        public List<Vector4> Pebbles { get; } = new();
        public List<GroundSprite> GroundSprites { get; } = new();
        public List<MarkerPlacement> Markers { get; } = new();
        /// <summary>Каменные блоки южной лестницы святилища: центр и размер (ступени — сложенные друг на друга блоки).</summary>
        public List<(Vector3 center, Vector3 size)> StairBlocks { get; } = new();

        readonly bool[] _walkable;
        /// <summary>Клетки, тип земли которых задан вручную (плато, ступени).</summary>
        readonly bool[] _locked;
        readonly bool[] _trail;
        /// <summary>Вершины, высоту которых задали вручную (плато, лестница): их не трогают тропы и сглаживание.</summary>
        readonly bool[] _lockedVertex;
        /// <summary>Вершины троп: сглаживание их почти не трогает, иначе дорога «расплывается».</summary>
        readonly bool[] _trailVertex;

        public SilverrunValeLayout()
        {
            Field = new TerrainField(-MarginWest, -MarginSouth,
                PlayWidth + MarginWest + MarginEast, PlayDepth + MarginSouth + MarginNorth, BottomHeight);
            _walkable = new bool[Field.Columns * Field.Rows];
            _locked = new bool[Field.Columns * Field.Rows];
            _trail = new bool[Field.Columns * Field.Rows];
            _lockedVertex = new bool[Field.VertexColumns * Field.VertexRows];
            _trailVertex = new bool[Field.VertexColumns * Field.VertexRows];

            // Рельеф: природная форма → холм святилища → мягкие поляны → тропы врезаются в склоны → лестница → брод.
            BuildBaseTerrain();
            BuildPlateau();
            BuildWoodlands();
            BuildGlades();
            BuildTrails();
            BuildStairs();
            BuildFord();
            Field.Smooth(2, (i, j) => _lockedVertex[VertexIndex(i, j)] ? 0f : _trailVertex[VertexIndex(i, j)] ? 0.15f : 0.5f);
            ClassifyGround();
            BuildWalkable();

            PlaceShoreTrees();
            PlaceForest();
            PlaceBushes();
            PlaceRocks();
            PlacePebbles();
            PlaceGroundSprites();
            PlacePointsOfInterest();
        }

        public bool IsWalkable(int c, int r) => Field.InBounds(c, r) && _walkable[r * Field.Columns + c];

        public bool IsWalkableAt(float x, float z) => Field.TryCell(x, z, out var c, out var r) && IsWalkable(c, r);

        // ---------- Травяной покров ----------

        /// <summary>
        /// Нужен ли слоям травы квад рельефа с левым нижним углом в вершине (i, j): есть ли рядом (с запасом в клетку —
        /// шейдер делает границы неровными) земля, на которой растёт трава, мох или редкие травинки.
        /// </summary>
        public bool QuadHasCover(int i, int j)
        {
            var cellsPerVertex = TerrainField.CellsPerUnit / TerrainField.VerticesPerUnit;
            for (var r = j * cellsPerVertex - 1; r <= (j + 1) * cellsPerVertex; r++)
            for (var c = i * cellsPerVertex - 1; c <= (i + 1) * cellsPerVertex; c++)
            {
                if (!Field.InBounds(c, r))
                    continue;
                if (Field.GetGround(c, r) is GroundType.Grass or GroundType.GrassLeaves or GroundType.ForestFloor or GroundType.Pebbles)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Высота травы в вершине (R цвета вершины, 0–1): на обрывах травы нет, у троп она примята,
        /// местами луг сочнее и выше.
        /// </summary>
        public Color32 GrassVertexColor(int i, int j)
        {
            var p = Field.VertexPosition(i, j);
            var scale = 0.78f * Mathf.Lerp(0.8f, 1.28f, Noise(p.x, p.y, 10f, 173));
            scale *= 1f - Smooth(30f, 42f, Field.SampleSlope(p.x, p.y));
            if (NearTrail(p, 0.6f))
                scale *= 0.45f;
            else if (NearTrail(p, 1.3f))
                scale *= 0.75f;
            var r = (byte)Mathf.RoundToInt(Mathf.Clamp01(scale) * 255f);
            return new Color32(r, 255, 255, 255);
        }

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
            : Mathf.Lerp(PassHeight, MeadowHeight, Smooth(PassZ, FieldZ, z));

        /// <summary>
        /// Непрерывная высота гор в юнитах: дно долин + хребты вдали от долин + скальные гребни.
        /// Ущелье Медвянки прорезает потом <see cref="CarveStream"/>.
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
            return Mathf.Max(MeadowHeight, ValleyFloor(z) + ridge + crag);
        }

        static float StreamDistance(float x, float z) => DistanceToPolyline(new Vector2(x, z), StreamPath, out _, out _) +
                                                         (Noise(x, z, 5f, 11) - 0.5f) * 0.8f;

        // ---------- Форма земли ----------

        /// <summary>Луг между рекой и лесом: пологие волны, редкие бугры, к опушке земля поднимается.</summary>
        static float MeadowLand(float x, float z)
        {
            var waves = (Noise(x, z, 14f, 29) - 0.5f) * 0.9f;
            var hummocks = Mathf.Max(0f, Noise(x, z, 5f, 31) - 0.6f) * 2.2f;
            return MeadowHeight + waves + hummocks + 0.7f * Smooth(ForestEdge(x, z) - 12f, ForestEdge(x, z), z);
        }

        /// <summary>Лес: холмы и ложбины, к Дубу-Прадеду земля в целом поднимается.</summary>
        static float ForestLand(float x, float z)
        {
            var hills = (Noise(x, z, 22f, 23) - 0.5f) * 6f;
            var rolls = (Noise(x, z, 8f, 24) - 0.5f) * 1.4f;
            return Mathf.Max(0.6f, 1.6f + 1.2f * Smooth(28f, 60f, z) + hills + rolls);
        }

        /// <summary>Поле у деревни: широкие пологие волны.</summary>
        static float FieldLand(float x, float z) => MeadowHeight + (Noise(x, z, 18f, 33) - 0.5f) * 1.1f;

        /// <summary>Нормированное расстояние до центра плато святилища (1 — край вершины холма).</summary>
        static float PlateauDistance(float x, float z)
        {
            var wobble = 1f + (Noise(x, z, 4f, 13) - 0.5f) * 0.12f;
            var dx = (x - PortalCenter.x) / (PlateauRadius.x * wobble);
            var dz = (z - PortalCenter.y) / (PlateauRadius.y * wobble);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float RavineDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            var best = float.MaxValue;
            foreach (var ravine in Ravines)
                best = Mathf.Min(best, DistanceToPolyline(p, ravine, out _, out _));
            return best + (Noise(x, z, 4f, 141) - 0.5f) * 1.2f;
        }

        /// <summary>
        /// Природная высота точки: луг → лес → горы → поле, затем вода прорезает своё: овраг, долина ручья, русло реки.
        /// Профили рассчитаны на вид сверху под углом: берега и стенки оврага местами крутые (обрывы видны),
        /// местами пологие (к воде можно спуститься).
        /// </summary>
        static float BaseHeight(float x, float z)
        {
            var edge = ForestEdge(x, z);
            var h = Mathf.Lerp(MeadowLand(x, z), ForestLand(x, z), Smooth(edge - 4f, edge + 7f, z));
            var m = MountainBlend(z);
            if (m > 0f)
                h = Mathf.Lerp(h, Mathf.Max(h, MountainHeight(x, z)), m);
            var field = FieldStart(x, z);
            h = Mathf.Lerp(h, FieldLand(x, z), Smooth(field - 2f, field + 4f, z));

            h = CarveRavine(x, z, h);
            h = CarveStream(x, z, h);
            return CarveRiver(x, z, h);
        }

        /// <summary>Ленточный овраг: ровное сырое дно и крутые стенки, местами — скальные.</summary>
        static float CarveRavine(float x, float z, float h)
        {
            var d = RavineDistance(x, z);
            // Овраг прорезает отрог: земля вдоль него приподнята, стенки выходят высокими.
            h += 1.4f * (1f - Smooth(4f, 12f, d));
            const float floorHalf = 2.4f;
            var wall = Mathf.Lerp(1.3f, 3f, Noise(x, z, 6f, 143));
            if (d > floorHalf + wall)
                return h;
            var floor = RavineFloor + (d / floorHalf) * (d / floorHalf) * 0.25f;
            var profile = d < floorHalf ? floor : Mathf.Lerp(RavineFloor + 0.25f, h, Smooth(floorHalf, floorHalf + wall, d));
            return Mathf.Min(h, profile);
        }

        /// <summary>
        /// Долина ручья Медвянки: русло, галечные отмели, луговое дно долины, склоны к окружающей земле.
        /// В горах склоны крутые — ущелье.
        /// </summary>
        static float CarveStream(float x, float z, float h)
        {
            var ds = StreamDistance(x, z);
            var valley = 6.5f + Noise(x, z, 7f, 19) * 2f;
            var wall = Mathf.Lerp(3f, 11f, MountainBlend(z)) * Mathf.Lerp(0.7f, 1.3f, Noise(x, z, 9f, 145));
            if (ds > valley + wall)
                return h;

            float profile;
            if (ds < 1.5f)
                profile = Mathf.Lerp(-0.6f, -0.35f, ds / 1.5f);
            else if (ds < 2.8f)
                profile = Mathf.Lerp(-0.35f, 0.3f, Smooth(1.5f, 2.8f, ds));
            else if (ds < valley)
                profile = Mathf.Lerp(0.3f, MeadowHeight, Smooth(2.8f, valley, ds)) + (Noise(x, z, 6f, 147) - 0.5f) * 0.3f;
            else
                profile = Mathf.Lerp(MeadowHeight, h, Smooth(valley, valley + wall, ds));
            return Mathf.Min(h, profile);
        }

        /// <summary>
        /// Серебрянка: глубокое русло, мелководье, южный галечный пляж и северный берег — широкая отмель,
        /// над ней земляной берег, где-то подмытый и крутой, где-то пологий.
        /// </summary>
        static float CarveRiver(float x, float z, float h)
        {
            var signed = z - RiverCenter(x) + (Noise(x, z, 6f, 3) - 0.5f) * 0.9f;
            var d = Mathf.Abs(signed);
            if (d < 3.8f)
            {
                var t = d / 3.8f;
                return Mathf.Min(h, Mathf.Lerp(-1.45f, -0.3f, Smooth(0.35f, 1f, t)));
            }

            float profile;
            if (signed < 0f)
            {
                profile = d < 5.5f
                    ? Mathf.Lerp(-0.3f, 0.3f, Smooth(3.8f, 5.5f, d))
                    : Mathf.Lerp(0.3f, h, Smooth(5.5f, 9f, d));
            }
            else
            {
                var bank = Mathf.Lerp(0.7f, 4f, Noise(x, z, 9f, 37));
                profile = d < 6.8f
                    ? Mathf.Lerp(-0.3f, 0.25f, Smooth(3.8f, 6.8f, d))
                    : Mathf.Lerp(0.25f, h, Smooth(6.8f, 6.8f + bank, d));
            }
            return Mathf.Min(h, profile);
        }

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

        static bool InPlateau(float x, float z, float scale = 1f) => PlateauDistance(x, z) <= scale;

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

        int VertexIndex(int i, int j) => j * Field.VertexColumns + i;

        // ---------- Рельеф ----------

        void BuildBaseTerrain()
        {
            for (var j = 0; j < Field.VertexRows; j++)
            for (var i = 0; i < Field.VertexColumns; i++)
            {
                var p = Field.VertexPosition(i, j);
                Field.SetVertexHeight(i, j, BaseHeight(p.x, p.y));
            }
        }

        /// <summary>Доля «горности»: 0 в лесу южнее дуба, 1 — в горах.</summary>
        static float MountainBlend(float z) => Smooth(MountainStartZ, MountainStartZ + 16f, z);

        static GroundType MountainGround(float x, float z, float height, float slope, float m)
        {
            if (height >= SnowLine + (Noise(x, z, 8f, 109) - 0.5f) * 3f)
                return GroundType.Snow;

            var rocky = slope > 34f || (height > 8.5f && Noise(x, z, 5f, 111) > 0.5f) || Noise(x, z, 4f, 113) > 0.74f;
            if (rocky && m > 0.4f)
                return GroundType.Rock;

            // Ниже — хвоя под редкими елями, выше — горный луг.
            if (m < 0.6f || (height < 5f && Noise(x, z, 6f, 115) > 0.5f))
                return GroundType.ForestFloor;
            return GroundType.Grass;
        }

        /// <summary>
        /// Холм святилища: ровная вершина-плато и крутые скальные бока. Наверх ведут лестница с юга
        /// (<see cref="BuildStairs"/>) и тропа с севера — её пандус врезает в склон <see cref="BuildTrails"/>.
        /// </summary>
        void BuildPlateau()
        {
            for (var j = 0; j < Field.VertexRows; j++)
            for (var i = 0; i < Field.VertexColumns; i++)
            {
                var p = Field.VertexPosition(i, j);
                var e = PlateauDistance(p.x, p.y);
                if (e > 1.45f)
                    continue;
                // Бока неровные — выступы скалы, а не идеальный конус.
                var shoulder = 1f + Mathf.Max(0f, Noise(p.x, p.y, 2.5f, 151) - 0.5f) * 0.36f;
                var natural = Mathf.Min(Field.GetVertexHeight(i, j), PlateauTop);
                Field.SetVertexHeight(i, j, Mathf.Lerp(PlateauTop, natural, Smooth(shoulder, 1.45f, e)));
                if (e <= 1f)
                    _lockedVertex[VertexIndex(i, j)] = true;
            }

            for (var r = 0; r < Field.Rows; r++)
            for (var c = 0; c < Field.Columns; c++)
            {
                var p = Field.CellCenter(c, r);
                if (!InPlateau(p.x, p.y))
                    continue;
                Field.SetGround(c, r, InFlagstones(p.x, p.y) ? GroundType.Flagstone : GroundType.Grass);
                _locked[CellIndex(c, r)] = true;
            }
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
                var seed = StableHash(w.id);
                var mean = MeanHeight(w.center, w.radius, (x, z) => BlobDistance(w.center, w.radius, seed, x, z) < 1f);
                SoftenAround(w.center, w.radius * 1.5f, (x, z) => BlobDistance(w.center, w.radius, seed, x, z), mean, 0.5f);
            }
        }

        /// <summary>Поляны: земля мягко сходится к средней высоте поляны, но лёгкие волны остаются — не стол.</summary>
        void BuildGlades()
        {
            foreach (var glade in Glades)
            {
                if (!glade.Flatten)
                    continue;
                var mean = MeanHeight(glade.Center, glade.Radius, (x, z) => GladeDistance(glade, x, z) < 1f);
                SoftenAround(glade.Center, glade.Radius * 1.5f, (x, z) => GladeDistance(glade, x, z), mean, 0.25f);
            }
        }

        float MeanHeight(Vector2 center, float radius, System.Func<float, float, bool> inside)
        {
            var sum = 0f;
            var n = 0;
            ForVerticesAround(center, radius * 1.5f, (i, j, p) =>
            {
                if (!inside(p.x, p.y))
                    return;
                sum += Field.GetVertexHeight(i, j);
                n++;
            });
            return n > 0 ? sum / n : Field.SampleHeight(center.x, center.y);
        }

        /// <summary>
        /// Внутри области (distance &lt; 0,85) перепады сжимаются к mean — остаётся доля keep; к краю (1,3) — плавный переход.
        /// Холм святилища и закреплённые вершины не трогаем.
        /// </summary>
        void SoftenAround(Vector2 center, float reach, System.Func<float, float, float> distance, float mean, float keep)
        {
            ForVerticesAround(center, reach, (i, j, p) =>
            {
                if (_lockedVertex[VertexIndex(i, j)] || PlateauDistance(p.x, p.y) < 1.5f)
                    return;
                var w = 1f - Smooth(0.85f, 1.3f, distance(p.x, p.y));
                if (w <= 0f)
                    return;
                var h = Field.GetVertexHeight(i, j);
                if (h < 0f)
                    return; // русла и отмели не поднимаем
                Field.SetVertexHeight(i, j, Mathf.Lerp(h, mean + (h - mean) * keep, w));
            });
        }

        /// <summary>
        /// Южная лестница святилища: пандус в рельефе (по нему идёт герой) и каменные блоки ступеней поверх.
        /// Рукотворная — единственное место с прямыми углами.
        /// </summary>
        void BuildStairs()
        {
            const float x0 = 38.6f, x1 = 43.4f, zTop = 18.9f, run = 0.55f, riseTarget = 0.3f;
            var estimate = zTop - Mathf.CeilToInt((PlateauTop - Field.SampleHeight(41f, 14.5f)) / riseTarget) * run;
            var bottom = Field.SampleHeight(41f, estimate - 0.5f);
            var rise = PlateauTop - bottom;
            var steps = Mathf.Max(2, Mathf.CeilToInt(rise / riseTarget));
            var stepRise = rise / steps;
            var zBottom = zTop - steps * run;

            // Пандус: вершины коридора лестницы — по прямой от подножия к плато.
            ForVerticesAround(new Vector2((x0 + x1) * 0.5f, (zBottom + zTop) * 0.5f), (zTop - zBottom) * 0.5f + 3f, (i, j, p) =>
            {
                if (p.x < x0 || p.x > x1 || p.y < zBottom - 0.6f || p.y > zTop + 0.6f)
                    return;
                Field.SetVertexHeight(i, j, Mathf.Lerp(bottom, PlateauTop, Mathf.InverseLerp(zBottom, zTop, p.y)));
                _lockedVertex[VertexIndex(i, j)] = true;
            });

            for (var z = zBottom; z < zTop; z += TerrainField.CellSize)
            for (var x = x0; x < x1; x += TerrainField.CellSize)
            {
                if (!Field.TryCell(x + 0.01f, z + 0.01f, out var c, out var r))
                    continue;
                Field.SetGround(c, r, GroundType.Flagstone);
                _locked[CellIndex(c, r)] = true;
            }

            // Ступени — блоки, сложенные «лесенкой»: каждый от подножия до верха плато, всё короче и выше.
            const float back = zTop + 0.4f;
            for (var k = 0; k < steps; k++)
            {
                var top = bottom + (k + 1) * stepRise;
                var front = zBottom + k * run;
                var floor = bottom - 0.5f;
                StairBlocks.Add((new Vector3((x0 + x1) * 0.5f, (top + floor) * 0.5f, (front + back) * 0.5f),
                    new Vector3(x1 - x0, top - floor, back - front)));
            }
        }

        void BuildTrails()
        {
            // Для каждой вершины — ближайшая тропа (по расстоянию до края), её высота и полуширина.
            var count = Field.VertexColumns * Field.VertexRows;
            var distance = new float[count];
            var target = new float[count];
            var halfWidth = new float[count];
            for (var k = 0; k < count; k++)
                distance[k] = float.MaxValue;

            foreach (var trail in Trails)
            {
                var samples = SampleTrail(trail);
                MarkTrailCells(trail, samples);
                CollectTrailVertices(trail, samples, distance, target, halfWidth);
            }

            // Тропа врезается в склон: полотно на высоте профиля тропы (чуть вогнутое — колея), обочины плавно
            // переходят в природный рельеф. Чем больше перепад, тем шире откос — выемки и насыпи, а не стенки.
            for (var j = 0; j < Field.VertexRows; j++)
            for (var i = 0; i < Field.VertexColumns; i++)
            {
                var k = VertexIndex(i, j);
                if (distance[k] == float.MaxValue || _lockedVertex[k])
                    continue;
                var h = Field.GetVertexHeight(i, j);
                if (h < 0f)
                    continue;
                var d = distance[k];
                var half = halfWidth[k];
                var shoulder = 0.9f + Mathf.Min(Mathf.Abs(h - target[k]), 2f) * 0.5f;
                if (d > half + shoulder)
                    continue;
                var w = d <= half ? 1f : 1f - Smooth(half, half + shoulder, d);
                var bed = target[k] - 0.06f * (1f - Mathf.Clamp01(d / half));
                Field.SetVertexHeight(i, j, Mathf.Lerp(h, bed, w));
                if (d <= half + 0.3f)
                    _trailVertex[k] = true;
            }
        }

        static float TrailHalfWidth(Trail trail, Vector2 p) => trail.Width * 0.5f + (Noise(p.x, p.y, 3f, 37) - 0.5f) * 1f;

        (Vector2 min, Vector2 max) TrailBounds(Trail trail, float reach)
        {
            var min = trail.Points[0];
            var max = trail.Points[0];
            foreach (var p in trail.Points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            var pad = Vector2.one * (trail.Width + reach + 4f); // запас на петляние кривой
            return (min - pad, max + pad);
        }

        void CollectTrailVertices(Trail trail, List<(Vector2 point, float height)> samples, float[] distance, float[] target, float[] halfWidth)
        {
            var (min, max) = TrailBounds(trail, 4f);
            Field.NearestVertex(min.x, min.y, out var i0, out var j0);
            Field.NearestVertex(max.x, max.y, out var i1, out var j1);
            for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var p = Field.VertexPosition(i, j);
                var nearest = NearestSample(samples, p, out var d);
                var half = TrailHalfWidth(trail, p);
                if (d > half + 2f)
                    continue;
                var k = VertexIndex(i, j);
                if (d - half >= distance[k] - halfWidth[k])
                    continue;
                distance[k] = d;
                target[k] = samples[nearest].height;
                halfWidth[k] = half;
            }
        }

        void MarkTrailCells(Trail trail, List<(Vector2 point, float height)> samples)
        {
            var (min, max) = TrailBounds(trail, 0f);
            for (var z = min.y; z <= max.y; z += TerrainField.CellSize)
            for (var x = min.x; x <= max.x; x += TerrainField.CellSize)
            {
                if (!Field.TryCell(x, z, out var c, out var r))
                    continue;
                var center = Field.CellCenter(c, r);
                NearestSample(samples, center, out var distance);
                if (distance > TrailHalfWidth(trail, center))
                    continue;
                var i = CellIndex(c, r);
                _walkable[i] = true;
                _trail[i] = true;
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

        /// <summary>Брод: глубокий омут в русле ручья, перейти его можно будет позже.</summary>
        void BuildFord()
        {
            var ford = System.Array.Find(Glades, g => g.Id == "stepstone_ford");
            ForVerticesAround(ford.Center, 3.5f, (i, j, p) =>
            {
                if (StreamDistance(p.x, p.y) > 1.6f || Vector2.Distance(p, ford.Center) > 2.3f)
                    return;
                Field.SetVertexHeight(i, j, -1.25f);
                _lockedVertex[VertexIndex(i, j)] = true;
            });
        }

        /// <summary>Тип земли каждой клетки — по высоте, крутизне и зоне; клетки плато и лестницы заданы заранее.</summary>
        void ClassifyGround()
        {
            for (var r = 0; r < Field.Rows; r++)
            for (var c = 0; c < Field.Columns; c++)
            {
                var i = CellIndex(c, r);
                if (_locked[i])
                    continue;
                var p = Field.CellCenter(c, r);
                Field.SetGround(c, r, GroundAt(p.x, p.y, _trail[i]));
            }
        }

        GroundType GroundAt(float x, float z, bool trail)
        {
            var h = Field.SampleHeight(x, z);
            if (h < DeepWaterHeight)
                return GroundType.DeepBed;
            if (h < WaterHeight + 0.02f)
                return GroundType.ShallowBed;
            if (trail)
                return GroundType.Dirt;

            // Отмели и пляжи: низкая земля у реки и ручья.
            if (h < 0.32f && (Mathf.Abs(z - RiverCenter(x)) < 8f || StreamDistance(x, z) < 3.4f))
                return GroundType.Pebbles;

            var slope = Field.SampleSlope(x, z);
            // Скальные бока холма святилища, стенки оврага, любые отвесные места — камень.
            // Подмытые берега и откосы троп остаются земляными: их рисует шейдер текстурой обрыва.
            var e = PlateauDistance(x, z);
            if (e > 1f && e < 1.55f && slope > 26f)
                return GroundType.Rock;
            if (RavineDistance(x, z) < 6f && slope > 34f && Noise(x, z, 3f, 149) > 0.3f)
                return GroundType.Rock;
            if (slope > 52f)
                return GroundType.Rock;

            foreach (var glade in Glades)
                if (GladeDistance(glade, x, z) < 1f)
                    return glade.Ground;

            var field = FieldStart(x, z);
            if (z > field)
                return GroundType.Grass;

            var m = MountainBlend(z);
            if (m > 0f)
                return MountainGround(x, z, h, slope, m);

            if (InWoodland(x, z))
                return Noise(x, z, 5f, 157) > 0.5f ? GroundType.GrassLeaves : GroundType.ForestFloor;

            var edge = ForestEdge(x, z);
            if (z > edge + 2f)
                return Noise(x, z, 7f, 159) > 0.62f ? GroundType.GrassLeaves : GroundType.ForestFloor;
            if (z > edge - 3f)
                return Noise(x, z, 4f, 161) > 0.45f ? GroundType.GrassLeaves : GroundType.Grass;
            return GroundType.Grass;
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

                _walkable[i] = (southZone || streamValley || field || InMountainZone(p.x, p.y) || inGlade || InWoodland(p.x, p.y)) &&
                               Field.SampleSlope(p.x, p.y) <= WalkableSlope;
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
                if (Field.SampleSlope(p.x, p.y) > 35f)
                    continue; // на обрывах и крутых откосах деревья не держатся

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
                    NearWalkable(p, 1.4f) || Vector2.Distance(p, GiantOakPosition) < 9f || Field.SampleSlope(p.x, p.y) > 35f)
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
                if (Field.SampleSlope(p.x, p.y) > 40f)
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
            // Скальные выступы на боках холма святилища и обломки у его подножия.
            for (var i = 0; i < 70; i++)
            {
                var a = PixelNoise.Hash01(i, 0, 163) * Mathf.PI * 2f;
                var e = 1.12f + PixelNoise.Hash01(i, 1, 163) * 0.4f;
                var x = PortalCenter.x + Mathf.Cos(a) * PlateauRadius.x * e;
                var z = PortalCenter.y + Mathf.Sin(a) * PlateauRadius.y * e;
                if (Mathf.Abs(x - 41f) < 3.2f && z < PortalCenter.y || NearTrail(new Vector2(x, z), 1.6f))
                    continue; // лестница и тропа свободны
                AddRock(x, z, 0.45f + PixelNoise.Hash01(i, 2, 163) * 0.9f);
            }

            // Стенки Ленточного оврага: валуны у подножия и выступы на склонах.
            for (var i = 0; i < 90; i++)
            {
                var x = 52f + PixelNoise.Hash01(i, 0, 167) * 24f;
                var z = 55f + PixelNoise.Hash01(i, 1, 167) * 21f;
                var d = RavineDistance(x, z);
                if (d < 2f || d > 5.5f || NearTrail(new Vector2(x, z), 1.4f))
                    continue;
                AddRock(x, z, 0.4f + PixelNoise.Hash01(i, 2, 167) * 0.8f);
            }
        }

        void AddRock(float x, float z, float scale)
        {
            if (!Field.TryCell(x, z, out _, out _))
                return;
            // Камень врастает в землю, на склоне — глубже: плоское основание не висит над откосом.
            var slope01 = Mathf.Clamp01(Field.SampleSlope(x, z) / 60f);
            Rocks.Add(new Vector4(x, Field.SampleHeight(x, z) - scale * (0.05f + slope01 * 0.3f), z, scale));
        }

        /// <summary>Галька у воды, камешки на тропах и в лесной подстилке — объёмные, отбрасывают тени.</summary>
        void PlacePebbles()
        {
            var i = 0;
            for (var z = Field.MinZ + 0.3f; z < Field.MaxZ; z += 0.6f)
            for (var x = Field.MinX + 0.3f; x < Field.MaxX; x += 0.6f)
            {
                i++;
                var px = x + (PixelNoise.Hash01(i, 0, 171) - 0.5f) * 0.55f;
                var pz = z + (PixelNoise.Hash01(i, 1, 171) - 0.5f) * 0.55f;
                if (!Field.TryCell(px, pz, out var c, out var r))
                    continue;
                var roll = PixelNoise.Hash01(i, 2, 171);
                var size = PixelNoise.Hash01(i, 3, 171);
                var (chance, min, max) = Field.GetGround(c, r) switch
                {
                    GroundType.Pebbles => (0.4f, 0.08f, 0.24f),
                    GroundType.ShallowBed => (0.22f, 0.1f, 0.3f),
                    GroundType.Dirt => (0.05f, 0.05f, 0.13f),
                    GroundType.ForestFloor => (0.012f, 0.08f, 0.18f),
                    GroundType.Rock => (0.03f, 0.1f, 0.3f),
                    _ => (0f, 0f, 0f),
                };
                if (roll >= chance)
                    continue;
                var scale = Mathf.Lerp(min, max, size * size);
                Pebbles.Add(new Vector4(px, Field.SampleHeight(px, pz) - scale * 0.15f, pz, scale));
            }
        }

        /// <summary>
        /// Растения поверх травяного покрова (сам покров — слои травы в шейдере): высокие сухие стебли, кустики,
        /// цветы, папоротники. Каждое — крест из 3D-карточек, как листва деревьев.
        /// </summary>
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
                    if (Field.SampleSlope(px, pz) > 38f)
                        continue;
                    var position = new Vector3(px, Field.SampleHeight(px, pz), pz);

                    switch (ground)
                    {
                        case GroundType.Grass:
                        case GroundType.GrassLeaves:
                            // Над сплошным покровом — редкие высокие стебли и кустики; цветы — россыпью, а не букетами.
                            if (nearRuins && roll < 0.08f)
                                AddSprite(position, FoliageSprites.Flowers);
                            else if (roll < 0.07f)
                                AddSprite(position, FoliageSprites.TuftDry);
                            else if (roll < 0.11f)
                                AddSprite(position, FoliageSprites.TuftGreen);
                            else if (roll > 0.988f)
                                AddSprite(position, FoliageSprites.Flowers);
                            break;
                        case GroundType.ForestFloor:
                            if (roll < 0.2f)
                                AddSprite(position, FoliageSprites.Fern);
                            else if (roll < 0.26f)
                                AddSprite(position, PixelNoise.Hash01(ix, iz, 83 + k) < 0.5f ? FoliageSprites.TuftDry : FoliageSprites.TuftGreen);
                            break;
                        case GroundType.Flagstone:
                            if (Field.GetHeight(c, r) >= PlateauTop - 0.05f && roll < 0.06f &&
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

        void ForVerticesAround(Vector2 center, float radius, System.Action<int, int, Vector2> action)
        {
            Field.NearestVertex(center.x - radius, center.y - radius, out var i0, out var j0);
            Field.NearestVertex(center.x + radius, center.y + radius, out var i1, out var j1);
            for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
                action(i, j, Field.VertexPosition(i, j));
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
