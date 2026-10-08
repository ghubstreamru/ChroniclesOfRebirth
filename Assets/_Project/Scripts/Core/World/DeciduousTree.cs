using System.Collections.Generic;
using ChroniclesOfRebirth.Core.Meshes;
using ChroniclesOfRebirth.Core.Pixel;
using UnityEngine;

namespace ChroniclesOfRebirth.Core.World
{
    /// <summary>
    /// Объёмное лиственное дерево: изогнутый ствол с наплывами у комля, поверхностные корни из наплывов,
    /// основные ветви и ветви второго порядка, веточки и крона из 3D-карточек с пучками листьев.
    /// Габарит кроны задаёт эллипсоид: ветви дорастают до его границы, на их концах — облака листвы,
    /// каждая гроздь листьев сидит на своей веточке, отходящей от ближайшей ветви.
    /// </summary>
    public static class DeciduousTree
    {
        static readonly Color32 White = new(255, 255, 255, 255);

        /// <summary>Половина стороны карточки листвы в юнитах (пиксель листвы того же размера, что у всего мира).</summary>
        const float CardHalf = FoliageSprites.LeafCardCell * 0.5f / FoliageSprites.PixelsPerUnit;

        /// <summary>Площадь поверхности облака листвы на одну гроздь листьев.</summary>
        const float AreaPerCluster = 1.2f;
        const int CardsPerCluster = 2;
        /// <summary>Предел гроздей на дерево (великан): дальше грозди реже, а не больше.</summary>
        const int MaxClusters = 2500;
        /// <summary>Развилка ствола — на этой доле <see cref="Shape.TrunkHeight"/>.</summary>
        const float ForkHeight = 0.72f;

        public struct Shape
        {
            /// <summary>Высота ствола: от неё отсчитывается центр кроны, развилка — на <see cref="ForkHeight"/> её доли.</summary>
            public float TrunkHeight;
            /// <summary>Радиус ствола у земли (без наплывов).</summary>
            public float TrunkRadius;
            /// <summary>Полуоси эллипсоида кроны.</summary>
            public Vector3 CrownRadius;
            /// <summary>Смещение центра кроны от вершины ствола (TrunkHeight).</summary>
            public Vector3 CrownOffset;
            /// <summary>Толстых ветвей-«рогов» от развилки; 0 — две или три.</summary>
            public int Limbs;
            /// <summary>Поверхностные корни (выходят из наплывов комля); 0 — только наплывы.</summary>
            public int Roots;
            public float RootLength;
        }

        struct Node
        {
            public Vector3 Position;
            public float Radius;
        }

        sealed class Context
        {
            public MeshBuilder Bark;
            public Vector3 CrownCenter;
            public Vector3 CrownRadius;
            public readonly List<Node> Nodes = new();
            /// <summary>Концы ветвей — центры облаков листвы.</summary>
            public readonly List<Vector3> Tips = new();
            public int Seed;
            int _n;

            public float Next() => PixelNoise.Hash01(Seed, _n++, 7919);
            public float Range(float a, float b) => a + (b - a) * Next();

            public Vector3 InsideUnitSphere()
            {
                for (var k = 0; k < 8; k++)
                {
                    var p = new Vector3(Next() * 2f - 1f, Next() * 2f - 1f, Next() * 2f - 1f);
                    if (p.sqrMagnitude <= 1f)
                        return p;
                }
                return Vector3.zero;
            }
        }

        /// <summary>Добавляет дерево; возвращает сферу кроны (xyz — центр, w — наибольшая полуось) для датчиков камеры.</summary>
        public static Vector4 Add(MeshBuilder bark, MeshBuilder leaves, MeshBuilder collision, Vector3 position, Shape shape, int seed)
        {
            var c = new Context { Bark = bark, Seed = seed, CrownRadius = shape.CrownRadius };
            var lean = new Vector3(c.Range(-1f, 1f), 0f, c.Range(-1f, 1f)) * 0.08f;
            c.CrownCenter = position + (Vector3.up + lean) * shape.TrunkHeight + shape.CrownOffset;

            var trunk = GrowTrunk(c, position, shape, out var lobes, out var lobePhase);
            // Коллизия — только ствол до развилки, грубее (8 граней): ветви и крону герой не задевает.
            collision?.AddTube(0, trunk.Points, trunk.Radii, 8, White, 0f,
                (ring, angle) => FlareAt(trunk.Points[ring].y - position.y, angle, shape.TrunkRadius, lobes, lobePhase));

            for (var k = 0; k < shape.Roots; k++)
                AddRoot(c, collision, position, shape, (k + 0.5f) / shape.Roots * Mathf.PI * 2f, lobes, lobePhase);

            // Ствол и главный рог — одна непрерывная трубка: через развилку без стыка и среза.
            var leader = GrowLimbs(c, trunk, shape);
            var stem = new Branch();
            stem.Points.AddRange(trunk.Points);
            stem.Radii.AddRange(trunk.Radii);
            for (var i = 1; leader != null && i < leader.Points.Count; i++)
            {
                stem.Points.Add(leader.Points[i]);
                stem.Radii.Add(leader.Radii[i]);
            }
            bark.AddTube(0, stem.Points, stem.Radii, shape.TrunkRadius > 0.8f ? 24 : 16, White, BarkSeed(c),
                (ring, angle) => FlareAt(stem.Points[ring].y - position.y, angle, shape.TrunkRadius, lobes, lobePhase));

            AddFoliage(c, leaves);

            var maxRadius = Mathf.Max(shape.CrownRadius.x, Mathf.Max(shape.CrownRadius.y, shape.CrownRadius.z));
            return new Vector4(c.CrownCenter.x, c.CrownCenter.y, c.CrownCenter.z, maxRadius);
        }

        sealed class Branch
        {
            public readonly List<Vector3> Points = new();
            public readonly List<float> Radii = new();

            public float Length
            {
                get
                {
                    var sum = 0f;
                    for (var i = 1; i < Points.Count; i++)
                        sum += Vector3.Distance(Points[i - 1], Points[i]);
                    return sum;
                }
            }

            /// <summary>Точка, направление и радиус на доле f длины ветви.</summary>
            public void Sample(float f, out Vector3 position, out Vector3 direction, out float radius)
            {
                var target = Length * Mathf.Clamp01(f);
                for (var i = 1; i < Points.Count; i++)
                {
                    var segment = Vector3.Distance(Points[i - 1], Points[i]);
                    if (target <= segment || i == Points.Count - 1)
                    {
                        var t = segment > 1e-5f ? Mathf.Clamp01(target / segment) : 0f;
                        position = Vector3.Lerp(Points[i - 1], Points[i], t);
                        direction = (Points[i] - Points[i - 1]).normalized;
                        radius = Mathf.Lerp(Radii[i - 1], Radii[i], t);
                        return;
                    }
                    target -= segment;
                }
                position = Points[0];
                direction = Vector3.up;
                radius = Radii[0];
            }
        }

        // ---------- Ствол и корни ----------

        /// <summary>
        /// Ствол от земли до развилки: у комля кольца чаще (там наплывы), выше — шаг 0,5 юнита.
        /// Слегка извивается и тянется в сторону центра кроны.
        /// </summary>
        static Branch GrowTrunk(Context c, Vector3 position, Shape shape, out int lobes, out float lobePhase)
        {
            lobes = shape.Roots > 0 ? shape.Roots : 5;
            lobePhase = c.Next() * Mathf.PI * 2f;

            var trunk = new Branch();
            // Ствол идёт к развилке — в сторону центра кроны (у старого дерева с крону набок — с наклоном).
            var top = position + (c.CrownCenter - position).normalized * (shape.TrunkHeight * ForkHeight);
            var p = position + Vector3.down * 0.3f;
            var dir = Vector3.up;
            var total = Vector3.Distance(p, top);
            var travelled = 0f;
            var wobblePhase = c.Next() * 10f;

            trunk.Points.Add(p);
            trunk.Radii.Add(shape.TrunkRadius);
            var step = 0.12f;
            while (travelled < total - 0.05f)
            {
                var toTop = top - p;
                // Плавная «змейка» по высоте + притяжение к развилке.
                var wobble = new Vector3(Mathf.Sin(travelled * 0.9f + wobblePhase), 0f, Mathf.Cos(travelled * 0.7f + wobblePhase * 1.3f)) * 0.12f;
                dir = (Vector3.Lerp(dir, toTop.normalized, 0.35f) + wobble).normalized;
                var length = Mathf.Min(step, toTop.magnitude);
                p += dir * length;
                travelled += length;
                step = Mathf.Min(0.5f, step + 0.08f);

                trunk.Points.Add(p);
                trunk.Radii.Add(Mathf.Lerp(shape.TrunkRadius, shape.TrunkRadius * 0.75f, travelled / total));
            }

            return trunk;
        }

        /// <summary>Наплывы комля: «лопасти» над корнями, быстро сходят на нет с высотой.</summary>
        static float FlareAt(float height, float angle, float radius, int lobes, float phase)
        {
            var fade = Mathf.Exp(-Mathf.Max(0f, height) / (radius * 1.4f + 0.15f));
            var lobe = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(lobes * angle + phase)), 2f);
            return 1f + fade * (0.25f + 0.55f * lobe);
        }

        /// <summary>Поверхностный корень: выходит из наплыва комля, изгибается и уходит в землю.</summary>
        static void AddRoot(Context c, MeshBuilder collision, Vector3 position, Shape shape, float slot, int lobes, float lobePhase)
        {
            // Угол на гребне ближайшего наплыва (угол трубки 0 — это +Z, 90° — это +X).
            var k = Mathf.Round((slot * lobes + lobePhase) / (Mathf.PI * 2f));
            var angle = (k * Mathf.PI * 2f - lobePhase) / lobes + c.Range(-0.12f, 0.12f);
            var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            var side = Vector3.Cross(Vector3.up, outward);
            var length = shape.RootLength * c.Range(0.7f, 1.25f);
            var r = shape.TrunkRadius;
            var swerve = c.Range(-0.3f, 0.3f);

            var root = new Branch();
            const int steps = 10;
            for (var s = 0; s <= steps; s++)
            {
                var f = s / (float)steps;
                // Высота: из ствола на уровне наплыва полого вниз, конец под землёй.
                var height = Mathf.Lerp(r * 1.2f + 0.2f, -0.3f, Mathf.Pow(f, 0.6f));
                var reach = r * 0.55f + length * f;
                root.Points.Add(position + outward * reach + side * (swerve * f * f * length) + Vector3.up * height);
                root.Radii.Add(Mathf.Lerp(r * 0.5f, 0.04f, Mathf.Pow(f, 0.8f)));
            }

            c.Bark.AddTube(0, root.Points, root.Radii, 8, White, BarkSeed(c));
            collision?.AddTube(0, root.Points, root.Radii, 5, White);
        }

        // ---------- Ветви ----------

        /// <summary>
        /// Развилка: ствол расходится на 2–3 толстые ветви-«рога» (у великана — больше) веером вверх-наружу.
        /// Первый рог — продолжение ствола (возвращается, ствол и он рисуются одной трубкой), остальные выходят
        /// из глубины ствола с утолщением у основания. От рогов отходят боковые ветви; на концах — облака листвы.
        /// </summary>
        static Branch GrowLimbs(Context c, Branch trunk, Shape shape)
        {
            var maxRadius = Mathf.Max(shape.CrownRadius.x, shape.CrownRadius.z);
            var scaffolds = shape.Limbs > 0 ? shape.Limbs : c.Next() < 0.5f ? 2 : 3;
            trunk.Sample(1f, out var fork, out var trunkDir, out var forkRadius);
            var azimuth = c.Next() * 360f;
            Branch leader = null;

            for (var k = 0; k < scaffolds; k++)
            {
                var isLeader = k == 0;
                // Главный рог отклоняется слабо, остальные — веером в другую сторону.
                azimuth += isLeader ? 0f : 360f / scaffolds + c.Range(-25f, 25f);
                var tilt = isLeader ? c.Range(10f, 24f) : c.Range(28f, 48f);
                var dir = (Quaternion.Euler(0f, azimuth, 0f) * (Quaternion.Euler(tilt, 0f, 0f) * Vector3.up) + trunkDir * 0.3f).normalized;
                var length = Mathf.Min(ToEnvelope(c, fork, dir) * c.Range(0.72f, 0.88f), maxRadius * 1.6f);
                if (length < 0.6f)
                    continue;

                Branch scaffold;
                float r0;
                if (isLeader)
                {
                    r0 = forkRadius;
                    scaffold = Grow(c, fork, dir, length, r0, Mathf.Max(0.03f, r0 * 0.2f), 0.05f, 0.1f, 0f, trunkDir);
                    leader = scaffold;
                }
                else
                {
                    // Основание спрятано в стволе: на поверхность ветвь выходит уже с плавным утолщением.
                    r0 = forkRadius * c.Range(0.62f, 0.75f);
                    var start = fork - trunkDir * (forkRadius * 0.9f);
                    scaffold = Grow(c, start, dir, length, r0, Mathf.Max(0.03f, r0 * 0.25f), 0.05f, 0.1f, 0.6f, null, forkRadius);
                    c.Bark.AddTube(0, scaffold.Points, scaffold.Radii, TubeSides(r0), White, BarkSeed(c));
                }

                AddNodes(c, scaffold, 0.4f);
                c.Tips.Add(scaffold.Points[^1]);
                GrowSideLimbs(c, scaffold, length, maxRadius);
            }

            return leader;
        }

        /// <summary>Боковые ветви рога: отходят на 40–70° и наружу от оси дерева.</summary>
        static void GrowSideLimbs(Context c, Branch scaffold, float scaffoldLength, float maxRadius)
        {
            var count = Mathf.Clamp(Mathf.RoundToInt(scaffoldLength / 0.9f), 2, 8);
            for (var k = 0; k < count; k++)
            {
                var f = Mathf.Lerp(0.25f, 0.9f, (k + c.Range(0.1f, 0.9f)) / count);
                scaffold.Sample(f, out var start, out var along, out var radius);

                var outward = start - c.CrownCenter;
                outward.y = 0f;
                var side = Vector3.Cross(along, c.InsideUnitSphere()).normalized + outward.normalized * 0.8f;
                side = Vector3.ProjectOnPlane(side, along).normalized;
                if (side.sqrMagnitude < 0.5f)
                    continue;
                var angle = c.Range(40f, 70f) * Mathf.Deg2Rad;
                var dir = (along * Mathf.Cos(angle) + side * Mathf.Sin(angle)).normalized;

                var length = Mathf.Min(ToEnvelope(c, start, dir) * c.Range(0.75f, 0.95f), scaffoldLength * 0.7f);
                if (length < 0.5f)
                    continue;

                var r0 = Mathf.Max(0.03f, radius * c.Range(0.5f, 0.7f));
                var limb = Grow(c, start, dir, length, r0, Mathf.Max(0.02f, r0 * 0.25f), 0.06f, 0.15f, 0.5f, null, radius);
                c.Bark.AddTube(0, limb.Points, limb.Radii, TubeSides(r0), White, BarkSeed(c));
                AddNodes(c, limb, 0.3f);
                c.Tips.Add(limb.Points[^1]);
                GrowSubBranches(c, limb, length);
            }
        }

        /// <summary>Сдвиг рисунка коры: у каждой ветви свой, иначе у всех деревьев кора была бы одинаковой.</summary>
        static float BarkSeed(Context c) => c.Next() * 64f;

        /// <summary>Граней у трубки: толстые ветви круглее, тонкие — экономнее.</summary>
        static int TubeSides(float radius) => radius > 0.25f ? 12 : radius > 0.1f ? 8 : 5;

        /// <summary>Мелкие ветви: густой каркас под листвой, к ним крепятся веточки гроздей.</summary>
        static void GrowSubBranches(Context c, Branch limb, float limbLength)
        {
            var count = Mathf.Clamp(Mathf.RoundToInt(limbLength / 0.7f), 2, 16);
            for (var k = 0; k < count; k++)
            {
                var f = Mathf.Lerp(0.25f, 0.95f, (k + c.Range(0.1f, 0.9f)) / count);
                limb.Sample(f, out var start, out var along, out var radius);

                // Отклонение от материнской ветви на 35–60° в случайную сторону, чуть вверх.
                var side = Vector3.Cross(along, c.InsideUnitSphere()).normalized;
                if (side.sqrMagnitude < 0.5f)
                    continue;
                var angle = c.Range(35f, 60f) * Mathf.Deg2Rad;
                var dir = (along * Mathf.Cos(angle) + side * Mathf.Sin(angle) + Vector3.up * 0.2f).normalized;

                var length = Mathf.Min(ToEnvelope(c, start, dir) * c.Range(0.75f, 1f), limbLength * 0.65f);
                if (length < 0.4f)
                    continue;

                var r0 = Mathf.Max(0.025f, radius * c.Range(0.5f, 0.7f));
                var branch = Grow(c, start, dir, length, r0, Mathf.Max(0.016f, r0 * 0.3f), 0.03f, 0.25f, 0.35f, null, radius);
                c.Bark.AddTube(0, branch.Points, branch.Radii, r0 > 0.06f ? 6 : 5, White, BarkSeed(c));
                AddNodes(c, branch, 0.2f);
            }
        }

        /// <summary>
        /// Ветвь из start по dir: изгибается к свету (tropism) и слегка петляет (wobble).
        /// collar — утолщение у основания (доля радиуса), сходит на нет за пару радиусов: ветвь «вырастает»
        /// из материнской без ступеньки; у основания кольца чаще, чтобы утолщение было плавным.
        /// from — направление в начале (продолжение ствола): ветвь плавно поворачивает от него к dir, без излома.
        /// parentRadius — радиус материнской ветви: внутри неё основание не толще её, утолщение не торчит кольцом.
        /// </summary>
        static Branch Grow(Context c, Vector3 start, Vector3 dir, float length, float r0, float r1, float tropism, float wobble,
            float collar = 0f, Vector3? from = null, float parentRadius = 0f)
        {
            var branch = new Branch();
            var step = length / Mathf.Clamp(Mathf.CeilToInt(length / 0.4f), 2, 16);
            var heading = from ?? dir;
            var p = start;
            var travelled = 0f;
            float Clamp(float r) => parentRadius > 0f ? Mathf.Min(r, parentRadius * 0.95f + travelled) : r;
            branch.Points.Add(p);
            branch.Radii.Add(Clamp(r0 * (1f + collar)));
            while (travelled < length - 1e-3f)
            {
                var ds = collar > 0f && travelled < r0 * 3f ? Mathf.Max(0.05f, r0 * 0.6f) : step;
                ds = Mathf.Min(ds, length - travelled);
                var share = ds / step;
                dir = (dir + (Vector3.up * tropism + c.InsideUnitSphere() * wobble) * share).normalized;
                heading = Vector3.Slerp(heading, dir, Mathf.Clamp01(share * 0.5f)).normalized;
                p += heading * ds;
                travelled += ds;

                var taper = Mathf.Lerp(r0, r1, Mathf.Pow(travelled / length, 0.8f));
                branch.Points.Add(p);
                branch.Radii.Add(Clamp(taper * (1f + collar * Mathf.Exp(-travelled / (r0 * 1.2f)))));
            }
            return branch;
        }

        static void AddNodes(Context c, Branch branch, float from)
        {
            var count = branch.Points.Count;
            for (var i = Mathf.CeilToInt(from * (count - 1)); i < count; i++)
                c.Nodes.Add(new Node { Position = branch.Points[i], Radius = branch.Radii[i] });
        }

        /// <summary>Расстояние от p по направлению d до границы эллипсоида кроны (0, если луч кроны не пересекает).</summary>
        static float ToEnvelope(Context c, Vector3 p, Vector3 d)
        {
            var inv = new Vector3(1f / c.CrownRadius.x, 1f / c.CrownRadius.y, 1f / c.CrownRadius.z);
            var o = Vector3.Scale(p - c.CrownCenter, inv);
            var v = Vector3.Scale(d, inv);
            var a = Vector3.Dot(v, v);
            var b = 2f * Vector3.Dot(o, v);
            var cc = Vector3.Dot(o, o) - 1f;
            var disc = b * b - 4f * a * cc;
            if (disc < 0f)
                return 0f;
            return Mathf.Max(0f, (-b + Mathf.Sqrt(disc)) / (2f * a));
        }

        // ---------- Листва ----------

        /// <summary>
        /// Крона — несколько «облаков» листвы на концах ветвей: между облаками просветы, в них видны сучья.
        /// Грозди листьев ложатся слоем по поверхности облаков (внутри соседнего облака — пропускаются),
        /// каждая сидит на веточке от ближайшей ветви и состоит из пары карточек под разными углами.
        /// Нормаль для света — смесь нормали облака и всей кроны: каждое облако объёмно, а крона читается целиком.
        /// В альфе цвета — затенение (низ облаков и глубина кроны, только для рассеянного света), в RGB — разброс оттенка.
        /// </summary>
        static void AddFoliage(Context c, MeshBuilder leaves)
        {
            var R = c.CrownRadius;
            var maxRadius = Mathf.Max(R.x, Mathf.Max(R.y, R.z));
            leaves.Anchor = new Vector4(c.CrownCenter.x, c.CrownCenter.y, c.CrownCenter.z, maxRadius + CardHalf);

            // Облака: на концах ветвей, чуть втянуты внутрь кроны, чтобы силуэт не выходил далеко за неё.
            var baseRadius = Mathf.Clamp(maxRadius * 0.32f, 0.7f, 2.8f);
            var masses = new List<Vector4>();
            foreach (var tip in c.Tips)
            {
                var r = baseRadius * c.Range(0.75f, 1.15f);
                var center = tip - (tip - c.CrownCenter).normalized * (r * 0.25f);
                masses.Add(new Vector4(center.x, center.y, center.z, r));
            }

            var totalArea = 0f;
            foreach (var m in masses)
                totalArea += 4f * Mathf.PI * m.w * m.w;
            // Перекрытия облаков «съедают» примерно половину поверхности — плотность с запасом, но в пределах бюджета.
            var areaPerCluster = Mathf.Max(AreaPerCluster, totalArea * 0.5f / MaxClusters);
            var golden = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (var mi = 0; mi < masses.Count; mi++)
            {
                var mass = masses[mi];
                var center = (Vector3)mass;
                var count = Mathf.Max(4, Mathf.RoundToInt(4f * Mathf.PI * mass.w * mass.w / areaPerCluster));
                var spin = c.Next() * Mathf.PI * 2f;

                for (var k = 0; k < count; k++)
                {
                    var y = 1f - (k + 0.5f) / count * 2f;
                    if (y < -0.55f)
                        continue; // низ облака открыт: снизу видны ветви
                    var ring = Mathf.Sqrt(1f - y * y);
                    var theta = golden * k + spin + c.Range(-0.3f, 0.3f);
                    var dir = new Vector3(Mathf.Cos(theta) * ring, y, Mathf.Sin(theta) * ring);
                    var cluster = center + dir * (mass.w * c.Range(0.78f, 1f));
                    if (InsideOther(masses, mi, cluster))
                        continue;

                    AddTwig(c, cluster);
                    var tint = c.Range(0.9f, 1.04f);
                    var warm = c.Range(-0.05f, 0.05f);
                    for (var n = 0; n < CardsPerCluster; n++)
                        AddCard(c, leaves, cluster + c.InsideUnitSphere() * 0.3f, dir, tint, warm, maxRadius);
                }
            }

            leaves.Anchor = null;
        }

        /// <summary>Гроздь глубоко внутри другого облака не видна — её не создаём.</summary>
        static bool InsideOther(List<Vector4> masses, int self, Vector3 p)
        {
            for (var j = 0; j < masses.Count; j++)
                if (j != self && Vector3.Distance(p, masses[j]) < masses[j].w * 0.82f)
                    return true;
            return false;
        }

        /// <summary>Веточка от ближайшей точки ветвей к грозди листьев.</summary>
        static void AddTwig(Context c, Vector3 cluster)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < c.Nodes.Count; i++)
            {
                var d = (c.Nodes[i].Position - cluster).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            if (best < 0 || bestDistance < 0.25f * 0.25f)
                return;

            var node = c.Nodes[best];
            var mid = Vector3.Lerp(node.Position, cluster, 0.5f) + Vector3.down * (Mathf.Sqrt(bestDistance) * 0.08f) + c.InsideUnitSphere() * 0.1f;
            var r0 = Mathf.Clamp(node.Radius * 0.7f, 0.016f, 0.035f);
            c.Bark.AddTube(0, new[] { node.Position, mid, cluster }, new[] { r0, r0 * 0.75f, 0.012f }, 4, White, BarkSeed(c));
        }

        static void AddCard(Context c, MeshBuilder leaves, Vector3 center, Vector3 massNormal, float tint, float warm, float maxRadius)
        {
            var R = c.CrownRadius;
            var local = center - c.CrownCenter;
            // Нормаль эллипсоида кроны (градиент): сплющенная крона освещается верно.
            var crownNormal = new Vector3(local.x / (R.x * R.x), local.y / (R.y * R.y), local.z / (R.z * R.z)).normalized;
            if (crownNormal.sqrMagnitude < 0.5f)
                crownNormal = Vector3.up;
            var outward = (massNormal * 0.6f + crownNormal * 0.4f).normalized;

            var face = (outward + c.InsideUnitSphere() * 0.9f).normalized;
            var right = Vector3.Cross(face, c.InsideUnitSphere() + Vector3.one * 0.01f).normalized;
            if (right.sqrMagnitude < 0.5f)
                right = Vector3.Cross(face, Vector3.right).normalized;
            var up = Vector3.Cross(right, face);
            var normal = Vector3.Lerp(face, outward, 0.75f).normalized;

            // Затенение: низ каждого облака и середина кроны получают меньше рассеянного света.
            var depth = Mathf.Clamp01(new Vector3(local.x / R.x, local.y / R.y, local.z / R.z).magnitude);
            var underside = Mathf.Clamp01(massNormal.y * 0.5f + 0.5f);
            var ao = Mathf.Lerp(0.55f, 1f, depth) * Mathf.Lerp(0.6f, 1f, underside);

            var color = new Color(tint * (1f + warm), tint, tint * (1f - warm), ao);
            var horizontal = new Vector2(local.x, local.z).magnitude / maxRadius;
            var wind = Mathf.Clamp01(0.3f + 0.7f * horizontal);

            var variant = Mathf.FloorToInt(c.Next() * FoliageSprites.LeafCardVariants) % FoliageSprites.LeafCardVariants;
            var w = 1f / FoliageSprites.LeafCardVariants;
            leaves.AddCard(0, center, right * CardHalf, up * CardHalf, normal, new Rect(variant * w, 0f, w, 1f), color, wind);
        }
    }
}

