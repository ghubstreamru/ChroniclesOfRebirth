using System.Collections.Generic;
using System.IO;
using ChroniclesOfRebirth.Core.Meshes;
using ChroniclesOfRebirth.Core.Pixel;
using ChroniclesOfRebirth.Core.World;
using ChroniclesOfRebirth.Gameplay.CameraSystem;
using ChroniclesOfRebirth.Gameplay.Player;
using ChroniclesOfRebirth.Gameplay.World;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChroniclesOfRebirth.Editor.Locations
{
    /// <summary>
    /// Собирает локацию «Долина Серебрянки»: генерирует текстуры, материалы и меши кодом и раскладывает их
    /// по сцене вместе с героем, камерой, светом, пост-обработкой и маркерами под будущие системы.
    /// Повторный запуск полностью пересобирает сгенерированные ассеты и сцену.
    /// </summary>
    public static partial class SilverrunValeBuilder
    {
        /// <summary>Утреннее солнце на северо-востоке — против камеры: контровой свет и лучи сквозь дымку.</summary>
        static readonly Quaternion SunRotation = Quaternion.Euler(32f, -115f, 0f);

        const string ScenePath = "Assets/_Project/Scenes/Loc_SilverrunVale.unity";
        const string GeneratedRoot = "Assets/_Project/Art/Generated/SilverrunVale";
        const string SpriteShader = "ChroniclesOfRebirth/PixelSprite";
        const string FoliageShader = "ChroniclesOfRebirth/PixelFoliage";
        const string BarkShader = "ChroniclesOfRebirth/PixelBark";
        const string WaterShader = "ChroniclesOfRebirth/PixelWater";
        const string TerrainBlendShader = "ChroniclesOfRebirth/TerrainBlend";
        const string LocationsTable = "Locations";
        /// <summary>Плитка текстур окружения: 128 px на 4 юнита = 32 px/юнит.</summary>
        const int TileSize = EnvironmentTextures.TileSize;
        const float CameraFieldOfView = 26f;

        static readonly Color FogColor = new(0.82f, 0.74f, 0.62f);
        static readonly Color32 White = new(255, 255, 255, 255);

        [MenuItem("Chronicles of Rebirth/Локации/Собрать «Долина Серебрянки»")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (AssetDatabase.IsValidFolder(GeneratedRoot))
                AssetDatabase.DeleteAsset(GeneratedRoot);
            EnsureFolder(GeneratedRoot + "/Textures");
            EnsureFolder(GeneratedRoot + "/Materials");
            EnsureFolder(GeneratedRoot + "/Meshes");

            var layout = new SilverrunValeLayout();
            var materials = CreateMaterials();

            var environment = new GameObject("Environment");
            BuildTerrain(layout, materials, environment.transform);
            BuildWater(layout, materials, environment.transform);
            BuildTrees(layout, materials, environment.transform);
            BuildBushes(layout, materials, environment.transform);
            BuildRocks(layout, materials, environment.transform);
            BuildRuins(materials, environment.transform);
            BuildGroundFoliage(layout, materials, environment.transform);
            BuildBlockers(layout, environment.transform);
            BuildMarkers(layout);

            var hero = BuildHero(layout, materials);
            BuildAtmosphere(layout, hero, environment.transform);
            BuildLighting();
            BuildCamera(hero);
            EnsureScreenEffects();
            EnsureLocationNames();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetMainScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Chronicles] Локация «Долина Серебрянки» собрана: {ScenePath}. " +
                      $"Деревьев: {layout.Trees.Count}, кустов: {layout.Bushes.Count}, маркеров: {layout.Markers.Count}.");
        }

        // ---------- Материалы ----------

        sealed class MaterialSet
        {
            public Material[] Terrain;
            public Material Water;
            public Material Bark;
            public Material Rock;
            public Material StoneWall;
            public Material Portal;
            public Material GroundFoliage;
            public Material[] Bush;
            public Material Hero;
            /// <summary>Для каждого вида кроны — пара: непрозрачный материал и материал бледнеющих крон.</summary>
            public readonly Dictionary<SilverrunValeLayout.TreeKind, Material[]> Canopy = new();
        }

        static MaterialSet CreateMaterials()
        {
            var set = new MaterialSet();

            set.Terrain = new Material[TerrainField.SubmeshCount];

            // Верхние грани всех типов земли рисует один материал смешивания: тип — из карты клеток,
            // границы — неровные (TerrainBlend.shader). Индекс слоя массива = GroundType.
            var layers = new Texture2D[(int)GroundType.Snow + 1];
            layers[(int)GroundType.DeepBed] = EnvironmentTextures.Riverbed(TileSize, 11);
            layers[(int)GroundType.ShallowBed] = EnvironmentTextures.Pebbles(TileSize, 12);
            layers[(int)GroundType.Pebbles] = EnvironmentTextures.Gravel(TileSize, 13);
            layers[(int)GroundType.Grass] = EnvironmentTextures.Grass(TileSize, 14);
            layers[(int)GroundType.GrassLeaves] = EnvironmentTextures.GrassWithLeaves(TileSize, 15);
            layers[(int)GroundType.ForestFloor] = EnvironmentTextures.ForestFloor(TileSize, 16);
            layers[(int)GroundType.Dirt] = EnvironmentTextures.Dirt(TileSize, 17);
            layers[(int)GroundType.Flagstone] = EnvironmentTextures.Flagstones(TileSize, 18);
            layers[(int)GroundType.Rock] = EnvironmentTextures.Rock(TileSize, 24);
            layers[(int)GroundType.Snow] = EnvironmentTextures.Snow(TileSize, 25);
            var blend = new Material(Shader.Find(TerrainBlendShader)) { name = "Ground_Blend" };
            blend.SetTexture("_GroundTextures", SaveTextureArray(layers, "GroundTextures"));
            blend.SetFloat("_UnitsPerTile", MeshBuilder.UvUnitsPerTile);
            blend.SetFloat("_TexelsPerUnit", EnvironmentTextures.TexelsPerUnit);
            blend.enableInstancing = true;
            SaveMaterial(blend);
            for (var i = 0; i < layers.Length; i++)
                set.Terrain[i] = blend;
            set.Terrain[TerrainField.SideEarth] = LitFrom("Side_Earth", EnvironmentTextures.CliffEarth(TileSize, 19));
            set.Terrain[TerrainField.SideRock] = LitFrom("Side_Rock", EnvironmentTextures.CliffRock(TileSize, 26));
            set.StoneWall = LitFrom("Stone_Wall", EnvironmentTextures.StoneWall(TileSize, 20));
            set.Terrain[TerrainField.SideStone] = set.StoneWall;
            // Кора считается в шейдере по 3D-позиции — без развёртки и швов на стволах и стыках ветвей.
            set.Bark = new Material(Shader.Find(BarkShader)) { name = "Bark", enableInstancing = true };
            set.Bark.SetTexture("_BarkRamp", SaveTexture(AutumnPalette.Bark.ToTexture("Bark_Palette")));
            set.Bark.SetFloat("_RampSize", AutumnPalette.Bark.Count);
            set.Bark.SetFloat("_TexelsPerUnit", EnvironmentTextures.TexelsPerUnit);
            SaveMaterial(set.Bark);
            set.Rock = LitFrom("Rock", EnvironmentTextures.Rock(TileSize, 22));

            var water = new Material(Shader.Find(WaterShader)) { name = "Water" };
            // Прозрачная бирюза у берега, плотный сине-зелёный вглубь; небо в отражении — тёплое утреннее.
            water.SetColor("_ShallowColor", new Color(0.72f, 0.95f, 0.9f, 1f));
            water.SetColor("_DeepColor", new Color(0.06f, 0.24f, 0.28f, 1f));
            water.SetColor("_SkyColor", FogColor);
            water.SetFloat("_PixelsPerUnit", EnvironmentTextures.TexelsPerUnit);
            set.Water = SaveMaterial(water);

            PortalTextures.Generate(160, 23, out var portalBase, out var portalEmission);
            SaveTexture(portalBase);
            SaveTexture(portalEmission);
            var portal = Lit("Portal_Circle", portalBase);
            portal.SetFloat("_AlphaClip", 1f);
            portal.SetFloat("_Cutoff", 0.5f);
            portal.EnableKeyword("_ALPHATEST_ON");
            portal.renderQueue = (int)RenderQueue.AlphaTest;
            portal.SetTexture("_EmissionMap", portalEmission);
            portal.SetColor("_EmissionColor", new Color(0.6f, 1.4f, 2.2f));
            portal.EnableKeyword("_EMISSION");
            portal.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            set.Portal = SaveMaterial(portal);

            // Лиственные кроны — объёмные, из 3D-карточек; в каждой кроне немного листьев соседнего оттенка.
            set.Canopy[SilverrunValeLayout.TreeKind.Orange] = FoliageMaterial("Orange", AutumnPalette.LeavesOrange, AutumnPalette.LeavesYellow, 31);
            set.Canopy[SilverrunValeLayout.TreeKind.Red] = FoliageMaterial("Red", AutumnPalette.LeavesRed, AutumnPalette.LeavesOrange, 32);
            set.Canopy[SilverrunValeLayout.TreeKind.Yellow] = FoliageMaterial("Yellow", AutumnPalette.LeavesYellow, AutumnPalette.LeavesOrange, 33);
            set.Canopy[SilverrunValeLayout.TreeKind.Conifer] =
                CanopyMaterial("Conifer", FoliageSprites.ConiferTierAtlas(AutumnPalette.LeavesConifer, 34, "Leaves_Conifer"));
            set.Bush = CanopyMaterial("Bush", AutumnPalette.LeavesBush, 35);

            var ground = SaveTexture(FoliageSprites.GroundAtlas(41));
            set.GroundFoliage = SaveMaterial(Sprite("GroundFoliage", ground, 1f, 0.35f, 0.5f, 0.06f, 0.3f));

            var hero = SaveTexture(HeroSpriteSheet.Generate());
            set.Hero = Sprite("Hero", hero, 0.7f, 0.55f, 0.7f, 0f, 0.2f);
            set.Hero.SetFloat("_AmbientStrength", 1.15f);
            // Кадр по умолчанию — «стоит лицом к камере»; в игре его переключает DirectionalSpriteAnimator.
            set.Hero.SetVector("_FrameRect", new Vector4(0f, 1f - 1f / HeroSpriteSheet.Directions,
                1f / HeroSpriteSheet.Frames, 1f / HeroSpriteSheet.Directions));
            // «Призрак» героя сквозь листву и геометрию; рисуется после окружения, чтобы глубина уже была.
            set.Hero.SetShaderPassEnabled("SRPDefaultUnlit", true);
            set.Hero.SetColor("_XRayColor", new Color(0.85f, 0.93f, 1f, 0.6f));
            set.Hero.renderQueue = (int)RenderQueue.AlphaTest + 10;
            SaveMaterial(set.Hero);

            return set;
        }

        static Material LitFrom(string name, Texture2D texture, string textureName = null) =>
            SaveMaterial(Lit(name, SaveTexture(texture, textureName)));

        /// <summary>
        /// Пара материалов кроны: непрозрачный (бледнеющие кроны пропускает) и полупрозрачный (рисует только их).
        /// Рендерер получает оба — меш с одним сабмешем рисуется каждым материалом по очереди.
        /// </summary>
        static Material[] CanopyMaterial(string name, ColorRamp ramp, int seed) =>
            CanopyMaterial(name, FoliageSprites.LeafClumpAtlas(ramp, seed, "Leaves_" + name));

        static Material[] CanopyMaterial(string name, Texture2D atlasTexture)
        {
            var atlas = SaveTexture(atlasTexture);

            var opaque = Sprite("Canopy_" + name, atlas, 0f, 0.3f, 0.45f, 0.03f, 1f);
            opaque.SetFloat("_CrownFade", 1f);
            opaque.SetFloat("_FadeMode", 0f);

            var faded = Sprite("Canopy_" + name + "_Faded", atlas, 0f, 0.3f, 0.45f, 0.03f, 1f);
            faded.SetFloat("_CrownFade", 1f);
            ConfigureFadedCanopy(faded);

            return new[] { SaveMaterial(opaque), SaveMaterial(faded) };
        }

        /// <summary>Пара материалов объёмной кроны (PixelFoliage) — по той же схеме, что и <see cref="CanopyMaterial(string, Texture2D)"/>.</summary>
        static Material[] FoliageMaterial(string name, ColorRamp ramp, ColorRamp accent, int seed)
        {
            var atlas = SaveTexture(FoliageSprites.LeafCardAtlas(ramp, accent, seed, "Leaves_" + name));

            Material Make(string materialName)
            {
                var material = new Material(Shader.Find(FoliageShader)) { name = materialName };
                material.SetTexture("_BaseMap", atlas);
                material.SetFloat("_CrownFade", 1f);
                material.enableInstancing = true;
                return material;
            }

            var opaque = Make("Foliage_" + name);
            opaque.SetFloat("_FadeMode", 0f);
            opaque.SetShaderPassEnabled("SRPDefaultUnlit", false);

            var faded = Make("Foliage_" + name + "_Faded");
            faded.SetShaderPassEnabled("DepthNormals", false);
            ConfigureFadedCanopy(faded);

            return new[] { SaveMaterial(opaque), SaveMaterial(faded) };
        }

        /// <summary>Бледный материал кроны: полупрозрачный, без теней и глубины, только бледнеющие кроны.</summary>
        static void ConfigureFadedCanopy(Material faded)
        {
            faded.SetFloat("_FadeMode", 1f);
            faded.SetFloat("_FadeOpacity", 0.3f);
            faded.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            faded.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            faded.SetFloat("_ZWrite", 0f);
            faded.SetShaderPassEnabled("ShadowCaster", false);
            faded.SetShaderPassEnabled("DepthOnly", false);
            // Предварительная глубина: пишется только ближайший слой листвы — прозрачность не накапливается.
            faded.SetShaderPassEnabled("SRPDefaultUnlit", true);
            faded.SetFloat("_PrePassZTest", (float)CompareFunction.LessEqual);
            faded.SetFloat("_PrePassZWrite", 1f);
            faded.SetFloat("_PrePassColorMask", 0f);
            faded.renderQueue = (int)RenderQueue.Transparent;
        }

        static Material Lit(string name, Texture2D texture)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            if (texture != null)
                material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.enableInstancing = true;
            return material;
        }

        static Material Sprite(string name, Texture2D texture, float upright, float normalBend, float wrap, float wind,
            float shadowOffset)
        {
            var material = new Material(Shader.Find(SpriteShader)) { name = name };
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Upright", upright);
            material.SetFloat("_NormalBend", normalBend);
            material.SetFloat("_Wrap", wrap);
            material.SetFloat("_WindStrength", wind);
            material.SetFloat("_ShadowOffset", shadowOffset);
            // Силуэт сквозь препятствия нужен только персонажам.
            material.SetShaderPassEnabled("SRPDefaultUnlit", false);
            material.enableInstancing = true;
            return material;
        }

        // ---------- Окружение ----------

        static void BuildTerrain(SilverrunValeLayout layout, MaterialSet materials, Transform parent)
        {
            // Карта типов земли для материала смешивания верхних граней.
            var field = layout.Field;
            var blend = materials.Terrain[(int)GroundType.Grass];
            blend.SetTexture("_GroundMap", SaveTexture(field.BuildGroundMap()));
            blend.SetVector("_MapRect", new Vector4(field.MinX, field.MinZ, field.MaxX - field.MinX, field.MaxZ - field.MinZ));
            blend.SetVector("_MapTexel", new Vector4(1f / field.Columns, 1f / field.Rows, field.Columns, field.Rows));
            EditorUtility.SetDirty(blend);

            var mesh = SaveMesh(layout.Field.BuildMesh("Terrain"));
            var go = CreateMeshObject("Terrain", parent, mesh, materials.Terrain);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            // Отдельный слой: по нему камера проверяет, не закрыл ли склон героя.
            go.layer = EnsureLayer(TerrainLayer);
        }

        static void BuildWater(SilverrunValeLayout layout, MaterialSet materials, Transform parent)
        {
            var builder = new MeshBuilder();
            var y = SilverrunValeLayout.WaterHeight;
            var f = layout.Field;
            builder.AddQuad(0,
                new Vector3(f.MinX, y, f.MinZ), new Vector3(f.MinX, y, f.MaxZ),
                new Vector3(f.MaxX, y, f.MaxZ), new Vector3(f.MaxX, y, f.MinZ),
                Vector2.zero, Vector2.up, Vector2.one, Vector2.right, White);
            var go = CreateMeshObject("Water", parent, SaveMesh(builder.Build("Water")), materials.Water);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Сторона участка, на которые делятся лиственные деревья: камера отсекает невидимые участки целиком.</summary>
        const float TreeChunkSize = 32f;

        /// <summary>Участок леса: кора всех его лиственных деревьев и листва по видам крон.</summary>
        sealed class TreeChunk
        {
            public readonly MeshBuilder Bark = new();
            public readonly Dictionary<SilverrunValeLayout.TreeKind, MeshBuilder> Leaves = new();
        }

        static void BuildTrees(SilverrunValeLayout layout, MaterialSet materials, Transform parent)
        {
            var trunks = new MeshBuilder();
            var collision = new MeshBuilder();
            var canopies = new Dictionary<SilverrunValeLayout.TreeKind, MeshBuilder>();
            var chunks = new Dictionary<Vector2Int, TreeChunk>();
            var root = new GameObject("Trees").transform;
            root.SetParent(parent, false);

            // Датчики крон: невидимые сферы-триггеры, по ним камера понимает, что кроны закрыли героя.
            var sensors = new GameObject("Canopy_Sensors");
            sensors.transform.SetParent(root, false);
            sensors.layer = EnsureLayer(CanopyLayer);
            void Sensor(Vector3 center, float radius)
            {
                var sphere = sensors.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.center = center;
                sphere.radius = radius;
            }

            var seed = 1000;
            foreach (var tree in layout.Trees)
            {
                seed += 17;
                var position = new Vector3(tree.Position.x, layout.Field.SampleHeight(tree.Position.x, tree.Position.y), tree.Position.y);
                var s = tree.Scale;

                if (tree.Kind == SilverrunValeLayout.TreeKind.Conifer)
                {
                    if (!canopies.TryGetValue(tree.Kind, out var canopy))
                        canopies[tree.Kind] = canopy = new MeshBuilder();
                    PropFactory.AddConifer(trunks, canopy, position, 7.8f * s, 1.9f * s, seed);
                    Sensor(position + Vector3.up * (4.6f * s), 2f * s);
                    continue;
                }

                var key = new Vector2Int(Mathf.FloorToInt(position.x / TreeChunkSize), Mathf.FloorToInt(position.z / TreeChunkSize));
                if (!chunks.TryGetValue(key, out var chunk))
                    chunks[key] = chunk = new TreeChunk();
                if (!chunk.Leaves.TryGetValue(tree.Kind, out var leaves))
                    chunk.Leaves[tree.Kind] = leaves = new MeshBuilder();

                var shape = DeciduousShape(tree, seed);
                var crown = DeciduousTree.Add(chunk.Bark, leaves, collision, position, shape, seed);
                // Сфера чуть меньше кроны: край листвы, задевший взгляд, ещё не повод уводить камеру.
                Sensor((Vector3)crown, Mathf.Max(shape.CrownRadius.x, shape.CrownRadius.z) * 0.8f);
            }

            // Ели (пока билборды): стволы — меш с коллизией, кроны — общий меш.
            var trunkMesh = SaveMesh(trunks.Build("Tree_Trunks"));
            // Кора без статической склейки: шейдер коры читает UV1/UV2, а склейка может их переписать.
            var trunkGo = CreateMeshObject("Trunks", root, trunkMesh, materials.Bark, false);
            trunkGo.AddComponent<MeshCollider>().sharedMesh = trunkMesh;
            foreach (var pair in canopies)
                CreateCanopyObjects("Canopy_" + pair.Key, root, SaveMesh(pair.Value.Build("Canopy_" + pair.Key)), materials.Canopy[pair.Key]);

            // Лиственные: по участкам. Ветви и крону герой не задевает — коллизия только у стволов и корней.
            foreach (var pair in chunks)
            {
                var suffix = $"{pair.Key.x}_{pair.Key.y}";
                CreateMeshObject("Bark_" + suffix, root, SaveMesh(pair.Value.Bark.Build("Bark_" + suffix)), materials.Bark, false);
                foreach (var leaves in pair.Value.Leaves)
                {
                    var name = $"Foliage_{leaves.Key}_{suffix}";
                    CreateCanopyObjects(name, root, SaveMesh(leaves.Value.Build(name)), materials.Canopy[leaves.Key]);
                }
            }

            var colliders = new GameObject("Trunk_Colliders");
            colliders.transform.SetParent(root, false);
            colliders.AddComponent<MeshCollider>().sharedMesh = SaveMesh(collision.Build("Trunk_Colliders"));
        }

        /// <summary>
        /// Форма лиственного дерева: обычное, старое (крона шире и сдвинута, корни) или великан.
        /// У обычных — разброс пропорций, чтобы соседние деревья не были копиями.
        /// </summary>
        static DeciduousTree.Shape DeciduousShape(SilverrunValeLayout.TreePlacement tree, int seed)
        {
            var s = tree.Scale;
            if (tree.Giant)
            {
                // Древо-великан: вдвое выше елей, крона шириной с поляну, корни-«пальцы» как стены.
                return new DeciduousTree.Shape
                {
                    TrunkHeight = 7f * s,
                    TrunkRadius = 1.5f * s,
                    CrownRadius = new Vector3(8.5f, 4.8f, 8f) * s,
                    CrownOffset = new Vector3(0f, 3.2f * s, 0f),
                    Limbs = 5,
                    Roots = 12,
                    RootLength = 4.2f * s,
                };
            }

            if (tree.Old)
            {
                return new DeciduousTree.Shape
                {
                    TrunkHeight = 3.2f * s,
                    TrunkRadius = 0.5f * s,
                    CrownRadius = new Vector3(3.2f, 2.1f, 2.9f) * s,
                    CrownOffset = new Vector3(0.4f, 1.1f * s, -1.3f),
                    Roots = 6,
                    RootLength = 2.1f,
                };
            }

            float Vary(int salt, float spread) => 1f + (PixelNoise.Hash01(seed, salt, 113) - 0.5f) * 2f * spread;
            var width = Vary(1, 0.15f);
            return new DeciduousTree.Shape
            {
                TrunkHeight = 3.8f * s * Vary(2, 0.12f),
                TrunkRadius = 0.45f * s * Vary(3, 0.12f),
                CrownRadius = new Vector3(2.5f * width * Vary(4, 0.08f), 2f * Vary(5, 0.15f), 2.3f * width * Vary(6, 0.08f)) * s,
                CrownOffset = new Vector3(0f, 1.4f * s, 0f),
                Roots = 4,
                RootLength = 0.9f * s,
            };
        }

        /// <summary>
        /// Крона рисуется двумя объектами с одним мешем: непрозрачный (с тенями) и бледный поверх
        /// (только кроны рядом с героем, полупрозрачно, без теней).
        /// </summary>
        static void CreateCanopyObjects(string name, Transform parent, Mesh mesh, Material[] pair)
        {
            // Без статического объединения: кроны и так единые меши, а склейка может терять канал UV2 с центром кроны.
            CreateMeshObject(name, parent, mesh, pair[0], false);
            var faded = CreateMeshObject(name + "_Faded", parent, mesh, pair[1], false);
            faded.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildBushes(SilverrunValeLayout layout, MaterialSet materials, Transform parent)
        {
            var builder = new MeshBuilder();
            var seed = 5000;
            foreach (var bush in layout.Bushes)
                PropFactory.AddBush(builder, bush, seed += 13);
            CreateCanopyObjects("Bushes", parent, SaveMesh(builder.Build("Bushes")), materials.Bush);
        }

        static void BuildRocks(SilverrunValeLayout layout, MaterialSet materials, Transform parent)
        {
            var builder = new MeshBuilder();
            var i = 0;
            foreach (var rock in layout.Rocks)
            {
                var scale = rock.w;
                var shape = new Vector3(
                    scale * (0.9f + PixelNoise.Hash01(i, 0, 91) * 0.5f),
                    scale * (0.55f + PixelNoise.Hash01(i, 1, 91) * 0.35f),
                    scale * (0.9f + PixelNoise.Hash01(i, 2, 91) * 0.4f));
                builder.AddRock(0, new Vector3(rock.x, rock.y, rock.z), shape, PixelNoise.Hash01(i, 3, 91) * 360f, i, White);
                i++;
            }

            var mesh = SaveMesh(builder.Build("Rocks"));
            var go = CreateMeshObject("Rocks", parent, mesh, materials.Rock);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static bool InPlateauEllipse(Vector3 p, float scale)
        {
            var dx = (p.x - SilverrunValeLayout.PortalCenter.x) / (SilverrunValeLayout.PlateauRadius.x * scale);
            var dz = (p.z - SilverrunValeLayout.PortalCenter.y) / (SilverrunValeLayout.PlateauRadius.y * scale);
            return dx * dx + dz * dz <= 1f;
        }

        static void BuildRuins(MaterialSet materials, Transform parent)
        {
            var root = new GameObject("Dawnstone_Shrine").transform;
            root.SetParent(parent, false);

            var stone = new MeshBuilder();
            var floor = SilverrunValeLayout.PlateauLevel * TerrainField.LevelHeight;
            var center = new Vector3(SilverrunValeLayout.PortalCenter.x, floor, SilverrunValeLayout.PortalCenter.y);

            // Кольцо колонн: 1, 5 и 6 целы (5–6 с перемычкой — «ворота»), 3 упала, остальные обломаны.
            const float ringRadius = 5.4f;
            var positions = new Vector3[8];
            for (var k = 0; k < 8; k++)
            {
                var angle = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                positions[k] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
            }

            for (var k = 0; k < 8; k++)
            {
                if (k == 3)
                {
                    var outward = (positions[k] - center).normalized;
                    var yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
                    stone.AddBox(0, Matrix4x4.TRS(positions[k] + Vector3.up * 0.15f, Quaternion.identity, Vector3.one),
                        new Vector3(1.15f, 0.5f, 1.15f), White);
                    PropFactory.AddFallenColumn(stone, positions[k] + outward * 0.8f, 3.2f, yaw);
                    continue;
                }

                var intact = k is 1 or 5 or 6;
                var height = intact ? 3.6f : 1.1f + PixelNoise.Hash01(k, 0, 97) * 1.4f;
                PropFactory.AddColumn(stone, positions[k], height, intact, 300 + k);
            }

            var lintelCenter = (positions[5] + positions[6]) * 0.5f + Vector3.up * 4.45f;
            var span = positions[6] - positions[5];
            stone.AddBox(0, Matrix4x4.TRS(lintelCenter, Quaternion.LookRotation(span.normalized, Vector3.up), Vector3.one),
                new Vector3(0.9f, 0.55f, span.magnitude + 1.1f), White);

            for (var i = 0; i < 12; i++)
            {
                var a = PixelNoise.Hash01(i, 0, 101) * Mathf.PI * 2f;
                var r = 3.8f + PixelNoise.Hash01(i, 1, 101) * 3.5f;
                var p = center + new Vector3(Mathf.Cos(a) * r * 1.2f, 0f, Mathf.Sin(a) * r * 0.85f);
                if (!InPlateauEllipse(p, 0.85f))
                    continue;
                var size = new Vector3(0.4f, 0.25f, 0.3f) * (1f + PixelNoise.Hash01(i, 2, 101) * 1.4f);
                var rotation = Quaternion.Euler(PixelNoise.Hash01(i, 3, 101) * 20f, PixelNoise.Hash01(i, 4, 101) * 360f, 0f);
                stone.AddBox(0, Matrix4x4.TRS(p + Vector3.up * size.y * 0.4f, rotation, Vector3.one), size, White);
            }

            AddWallArc(stone, center, 200f, 250f, 3);
            AddWallArc(stone, center, 292f, 335f, 7);
            AddWallArc(stone, center, 105f, 130f, 11);

            var stoneMesh = SaveMesh(stone.Build("Shrine_Stone"));
            var stoneGo = CreateMeshObject("Stone", root, stoneMesh, materials.StoneWall);
            stoneGo.AddComponent<MeshCollider>().sharedMesh = stoneMesh;

            var portal = new MeshBuilder();
            const float half = 5f; // текстура 160 px = 10 юнитов
            var y = floor + 0.02f;
            portal.AddQuad(0,
                new Vector3(center.x - half, y, center.z - half), new Vector3(center.x - half, y, center.z + half),
                new Vector3(center.x + half, y, center.z + half), new Vector3(center.x + half, y, center.z - half),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), White);
            var portalGo = CreateMeshObject("Portal_Circle", root, SaveMesh(portal.Build("Portal_Circle")), materials.Portal);
            portalGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            AddPointLight(root, "Portal_Glow", center + Vector3.up * 0.8f, new Color(0.45f, 0.8f, 1f), 3f, 7f);
        }

        static void AddWallArc(MeshBuilder stone, Vector3 center, float fromDeg, float toDeg, int seed)
        {
            var i = 0;
            for (var a = fromDeg; a < toDeg; a += 6f, i++)
            {
                var rad = a * Mathf.Deg2Rad;
                var r = new Vector2(SilverrunValeLayout.PlateauRadius.x - 1.1f, SilverrunValeLayout.PlateauRadius.y - 1f);
                var p = center + new Vector3(Mathf.Cos(rad) * r.x, 0f, Mathf.Sin(rad) * r.y);
                var tangent = new Vector3(-Mathf.Sin(rad) * r.x, 0f, Mathf.Cos(rad) * r.y).normalized;
                var height = 0.5f + PixelNoise.Hash01(seed, i, 103) * 0.9f;
                stone.AddBox(0, Matrix4x4.TRS(p + Vector3.up * height * 0.5f, Quaternion.LookRotation(tangent, Vector3.up), Vector3.one),
                    new Vector3(0.6f, height, 0.95f), White);
            }
        }

        static void BuildGroundFoliage(SilverrunValeLayout layout, MaterialSet materials, Transform parent)
        {
            CreateMeshObject("Ground_Foliage", parent, SaveMesh(BuildSpriteMesh("Ground_Foliage", layout.GroundSprites)), materials.GroundFoliage)
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static Mesh BuildSpriteMesh(string name, List<SilverrunValeLayout.GroundSprite> sprites)
        {
            var builder = new MeshBuilder();
            var w = 1f / FoliageSprites.GroundVariants;
            var size = FoliageSprites.GroundCell / (float)FoliageSprites.PixelsPerUnit;
            var i = 0;
            foreach (var sprite in sprites)
            {
                // Разные размер и оттенок у каждого пучка: покров неровный, без одинаковых копий.
                var scale = 0.7f + PixelNoise.Hash01(i, 1, 107) * 0.55f;
                var light = 0.82f + PixelNoise.Hash01(i, 0, 107) * 0.18f;
                var warm = (PixelNoise.Hash01(i, 2, 107) - 0.5f) * 0.12f;
                i++;
                builder.AddBillboard(0, sprite.Position, new Vector2(size, size) * scale, new Rect(sprite.Variant * w, 0f, w, 1f),
                    new Color(light * (1f + warm), light, light * (1f - warm), 1f));
            }
            return builder.Build(name);
        }

        /// <summary>Невидимые стены по краю проходимой области и по границе игровой зоны.</summary>
        static void BuildBlockers(SilverrunValeLayout layout, Transform parent)
        {
            var builder = new MeshBuilder();
            foreach (var (center, size) in layout.BuildBlockerBoxes())
                builder.AddBox(0, Matrix4x4.Translate(center), size, White, false);

            // Стены по границе зоны выше самых высоких гор.
            float w = SilverrunValeLayout.PlayWidth, d = SilverrunValeLayout.PlayDepth;
            const float wallY = 25f, wallH = 60f;
            builder.AddBox(0, Matrix4x4.Translate(new Vector3(-0.5f, wallY, d * 0.5f)), new Vector3(1f, wallH, d), White, false);
            builder.AddBox(0, Matrix4x4.Translate(new Vector3(w + 0.5f, wallY, d * 0.5f)), new Vector3(1f, wallH, d), White, false);
            builder.AddBox(0, Matrix4x4.Translate(new Vector3(w * 0.5f, wallY, -0.5f)), new Vector3(w, wallH, 1f), White, false);
            builder.AddBox(0, Matrix4x4.Translate(new Vector3(w * 0.5f, wallY, d + 0.5f)), new Vector3(w, wallH, 1f), White, false);

            var go = new GameObject("Blockers");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshCollider>().sharedMesh = SaveMesh(builder.Build("Blockers"));
        }

        static void BuildMarkers(SilverrunValeLayout layout)
        {
            var root = new GameObject("Markers").transform;
            foreach (var m in layout.Markers)
            {
                var go = new GameObject($"{m.Kind}_{m.Id}");
                go.transform.SetParent(root, false);
                go.transform.position = m.Position;
                go.AddComponent<WorldMarker>().Setup(m.Kind, m.Id, m.Radius, m.NameKey, m.Note);
            }
        }

        // ---------- Герой, свет, камера ----------

        static GameObject BuildHero(SilverrunValeLayout layout, MaterialSet materials)
        {
            var hero = new GameObject("Hero");
            hero.transform.position = SilverrunValeLayout.HeroSpawn;

            var controller = hero.AddComponent<CharacterController>();
            controller.height = 1.4f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, 0.72f, 0f);
            controller.stepOffset = 0.55f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.05f;

            var player = hero.AddComponent<PlayerController>();

            var builder = new MeshBuilder();
            var size = new Vector2(HeroSpriteSheet.FrameWidth, HeroSpriteSheet.FrameHeight) / HeroSpriteSheet.PixelsPerUnit;
            builder.AddBillboard(0, Vector3.zero, size, new Rect(0f, 0f, 1f, 1f), White);
            var sprite = CreateMeshObject("Sprite", hero.transform, SaveMesh(builder.Build("Hero_Quad")), materials.Hero, false);

            var animator = hero.AddComponent<DirectionalSpriteAnimator>();
            SetField(animator, "targetRenderer", sprite.GetComponent<MeshRenderer>());
            SetField(animator, "source", player);

            var bounds = new Vector4(layout.Field.MinX, layout.Field.MinZ,
                layout.Field.MaxX - layout.Field.MinX, layout.Field.MaxZ - layout.Field.MinZ);
            hero.AddComponent<CanopyFadeController>().Setup(SaveTexture(BuildDensityMap(layout)), bounds);
            return hero;
        }

        /// <summary>
        /// Карта густоты леса: 1 пиксель = 2×2 юнита, значение — доля непроходимых клеток (чаща), слегка размытая.
        /// По ней CanopyFadeController расширяет радиус бледнения крон в густом лесу.
        /// </summary>
        static Texture2D BuildDensityMap(SilverrunValeLayout layout)
        {
            const int unitsPerPixel = 2;
            var field = layout.Field;
            var cellsPerPixel = unitsPerPixel * TerrainField.CellsPerUnit;
            var w = field.Columns / cellsPerPixel;
            var h = field.Rows / cellsPerPixel;
            var raw = new float[w * h];

            for (var py = 0; py < h; py++)
            for (var px = 0; px < w; px++)
            {
                var blocked = 0;
                for (var r = py * cellsPerPixel; r < (py + 1) * cellsPerPixel; r++)
                for (var c = px * cellsPerPixel; c < (px + 1) * cellsPerPixel; c++)
                    if (!layout.IsWalkable(c, r))
                        blocked++;
                raw[py * w + px] = blocked / (float)(cellsPerPixel * cellsPerPixel);
            }

            var pixels = new Color32[w * h];
            for (var py = 0; py < h; py++)
            for (var px = 0; px < w; px++)
            {
                var sum = 0f;
                var n = 0;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var x = Mathf.Clamp(px + dx, 0, w - 1);
                    var y = Mathf.Clamp(py + dy, 0, h - 1);
                    sum += raw[y * w + x];
                    n++;
                }
                var v = (byte)Mathf.RoundToInt(sum / n * 255f);
                pixels[py * w + px] = new Color32(v, v, v, 255);
            }

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Canopy_Density",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static void BuildLighting()
        {
            var lighting = new GameObject("Lighting");

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(lighting.transform, false);
            sunGo.transform.rotation = SunRotation;
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.83f, 0.64f);
            sun.intensity = 3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.80f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.60f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.27f, 0.20f);
            RenderSettings.reflectionIntensity = 0.3f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            // Основную дымку рисует объёмный свет; встроенный туман только прячет дальний край мира.
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = FogColor;

            var volumeGo = new GameObject("Global_Volume");
            volumeGo.transform.SetParent(lighting.transform, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = CreateVolumeProfile();
        }

        static VolumeProfile CreateVolumeProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "SilverrunVale_MorningGoldenHour";

            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            // Только настоящие источники (огоньки, портал, фонари): светлые поверхности вроде снега не «плывут».
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.8f));

            // Эффект миниатюры делает экранный tilt-shift (EnsureTiltShift), а не глубина резкости.

            var colors = profile.Add<ColorAdjustments>(true);
            colors.postExposure.Override(0.25f);
            colors.contrast.Override(16f);
            colors.saturation.Override(14f);

            // Тени уходят в бирюзу, света — в тёплое золото, средние тона чуть тёплые: «дорогой» свет эталона.
            var tones = profile.Add<ShadowsMidtonesHighlights>(true);
            tones.shadows.Override(new Vector4(0.9f, 1f, 1.1f, -0.03f));
            tones.midtones.Override(new Vector4(1.02f, 1f, 0.97f, 0f));
            tones.highlights.Override(new Vector4(1.06f, 1f, 0.9f, 0f));

            var split = profile.Add<SplitToning>(true);
            // Бирюзовые тени против тёплого света — главный контраст эталона.
            split.shadows.Override(new Color(0.34f, 0.56f, 0.6f));
            split.balance.Override(-15f);
            split.highlights.Override(new Color(0.62f, 0.52f, 0.42f));

            profile.Add<WhiteBalance>(true).temperature.Override(12f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.15f);

            AssetDatabase.CreateAsset(profile, GeneratedRoot + "/SilverrunVale_Volume.asset");
            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            return profile;
        }

        static void BuildCamera(GameObject hero)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FogColor;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 140f;
            camera.fieldOfView = CameraFieldOfView;
            go.AddComponent<AudioListener>();

            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;

            // Камерой управляет Cinemachine: мозг на камере, ракурсы — виртуальные камеры.
            // Переезды между ракурсами (зоны камеры) — плавные, с замедлением в начале и в конце.
            var brain = go.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.6f);

            var cameras = new GameObject("Cameras").transform;
            CreateFollowCamera("CM_Follow", cameras, hero.transform, camera, CameraFieldOfView, 10);

            // Зоны камеры: свой ракурс, пока герой внутри; направление камеры везде одно.
            foreach (var zone in SilverrunValeLayout.CameraZones)
            {
                var follow = CreateFollowCamera("CM_Zone_" + zone.Id, cameras, hero.transform, camera, zone.FieldOfView, zone.Priority);
                follow.ConfigureFixed(zone.Pitch, zone.Distance);
                follow.gameObject.AddComponent<CameraZone>().Setup(hero.transform, zone.Center, zone.Radius);
                follow.GetComponent<CinemachineCamera>().enabled = false; // включает CameraZone, когда герой внутри
            }

            SetField(hero.GetComponent<PlayerController>(), "cameraTransform", go.transform);
        }

        /// <summary>Виртуальная камера Cinemachine с HD-2D следованием за героем (обход склонов, уход под кроны).</summary>
        static Hd2dFollow CreateFollowCamera(string name, Transform parent, Transform hero, Camera camera, float fieldOfView, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Follow = hero;
            var lens = LensSettings.Default;
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = camera.nearClipPlane;
            lens.FarClipPlane = camera.farClipPlane;
            vcam.Lens = lens;
            vcam.Priority = priority;

            var follow = go.AddComponent<Hd2dFollow>();
            follow.Configure(new Vector4(6f, 0f, SilverrunValeLayout.PlayWidth - 6f, SilverrunValeLayout.PlayDepth),
                1 << EnsureLayer(TerrainLayer), 1 << EnsureLayer(CanopyLayer));
            return follow;
        }

        // ---------- Tilt-shift ----------

        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string RenderingSettingsRoot = "Assets/_Project/Settings/Rendering";
        const string TiltShiftFeatureName = "TiltShift";
        const string VolumetricFeatureName = "VolumetricLight";

        /// <summary>
        /// Экранные эффекты перед пост-обработкой, по порядку: объёмный свет, затем tilt-shift
        /// (лучи тоже размываются по краям кадра, как всё остальное).
        /// </summary>
        static void EnsureScreenEffects()
        {
            EnsureShadowsAndOcclusion();

            var volumetric = EnsureFullScreenFeature(VolumetricFeatureName, "ChroniclesOfRebirth/VolumetricLight",
                ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            // Тени солнца в полноэкранном проходе: каскады включаем на самом материале.
            volumetric?.EnableKeyword("_MAIN_LIGHT_SHADOWS_CASCADE");
            EnsureFullScreenFeature(TiltShiftFeatureName, "ChroniclesOfRebirth/TiltShift", ScriptableRenderPassInput.Color);
            OrderFeatures(VolumetricFeatureName, TiltShiftFeatureName);
        }

        /// <summary>
        /// Подключает полноэкранный эффект к рендереру ПК как Full Screen Pass перед пост-обработкой.
        /// Материал хранится вне папки сгенерированных ассетов: пересборка локации не сбрасывает его настройки.
        /// </summary>
        static Material EnsureFullScreenFeature(string featureName, string shaderName, ScriptableRenderPassInput requirements)
        {
            EnsureFolder(RenderingSettingsRoot);
            var materialPath = $"{RenderingSettingsRoot}/{featureName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(shaderName)) { name = featureName };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[Chronicles] Не найден рендерер {RendererPath}, эффект {featureName} не подключён.");
                return null;
            }

            var feature = rendererData.rendererFeatures.Find(f => f != null && f.name == featureName) as FullScreenPassRendererFeature;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = featureName;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var so = new SerializedObject(rendererData);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = requirements;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            return material;
        }

        /// <summary>
        /// Тени и затенение щелей. Чёткая карта теней даёт «кружево» солнечных пятен под кронами, по ней же
        /// объёмный свет рисует лучи; SSAO затемняет щели, корни, подножия уступов — объём, как в эталоне.
        /// </summary>
        static void EnsureShadowsAndOcclusion()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                pipeline.mainLightShadowmapResolution = 4096;
                // Копия кадра без прозрачных объектов — по ней вода рисует преломлённое дно.
                pipeline.supportsCameraOpaqueTexture = true;
                var settings = new SerializedObject(pipeline);
                settings.FindProperty("m_OpaqueDownsampling").enumValueIndex = 0; // без уменьшения — дно в воде чёткое
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var ssao = rendererData != null ? rendererData.rendererFeatures.Find(f => f != null && f.name == "ScreenSpaceAmbientOcclusion") : null;
            if (ssao == null)
            {
                Debug.LogWarning("[Chronicles] В рендерере нет SSAO — затенение щелей не настроено.");
                return;
            }

            var so = new SerializedObject(ssao);
            so.FindProperty("m_Settings.Intensity").floatValue = 1.1f;
            so.FindProperty("m_Settings.Radius").floatValue = 0.5f;
            so.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.4f;
            so.FindProperty("m_Settings.Falloff").floatValue = 60f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ssao.SetActive(true);
            EditorUtility.SetDirty(rendererData);
        }

        /// <summary>Эффекты с одной точкой внедрения выполняются в порядке списка: ставим first перед second.</summary>
        static void OrderFeatures(string first, string second)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
                return;

            var so = new SerializedObject(rendererData);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            int IndexOf(string name)
            {
                for (var i = 0; i < features.arraySize; i++)
                    if (features.GetArrayElementAtIndex(i).objectReferenceValue is { } o && o.name == name)
                        return i;
                return -1;
            }

            var a = IndexOf(first);
            var b = IndexOf(second);
            if (a < 0 || b < 0 || a < b)
                return;
            features.MoveArrayElement(a, b);
            map.MoveArrayElement(a, b);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
        }

        // ---------- Локализация ----------

        /// <summary>Названия мест в таблице Locations (ru — исходный язык, en — перевод).</summary>
        static void EnsureLocationNames()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocationsTable) ??
                             LocalizationEditorSettings.CreateStringTableCollection(LocationsTable, "Assets/_Project/Localization/Tables");

            foreach (var place in SilverrunValeLayout.AllNames())
            foreach (var table in collection.StringTables)
            {
                var value = table.LocaleIdentifier.Code.StartsWith("ru") ? place.Ru : place.En;
                var entry = table.GetEntry(place.Key) ?? table.AddEntry(place.Key, value);
                entry.Value = value;
                EditorUtility.SetDirty(table);
            }

            EditorUtility.SetDirty(collection.SharedData);
        }

        // ---------- Утилиты ----------

        const string TerrainLayer = "Terrain";
        const string CanopyLayer = "Canopy";

        /// <summary>Пользовательский слой с таким именем: находит существующий или занимает первый свободный (8–31).</summary>
        static int EnsureLayer(string name)
        {
            var existing = LayerMask.NameToLayer(name);
            if (existing >= 0)
                return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (var i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue))
                    continue;
                layer.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }

            Debug.LogError($"[Chronicles] Нет свободного слоя для «{name}».");
            return 0;
        }

        static void AddPointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material, bool isStatic = true) =>
            CreateMeshObject(name, parent, mesh, new[] { material }, isStatic);

        static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material[] materials, bool isStatic = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            if (isStatic)
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Texture2D SaveTexture(Texture2D texture, string name = null)
        {
            if (name != null)
                texture.name = name;
            AssetDatabase.CreateAsset(texture, $"{GeneratedRoot}/Textures/{texture.name}.asset");
            return texture;
        }

        static Material SaveMaterial(Material material)
        {
            AssetDatabase.CreateAsset(material, $"{GeneratedRoot}/Materials/{material.name}.mat");
            return material;
        }

        /// <summary>Массив текстур одного размера (слой = индекс), Point, повтор.</summary>
        static Texture2DArray SaveTextureArray(Texture2D[] layers, string name)
        {
            var size = layers[0].width;
            var array = new Texture2DArray(size, size, layers.Length, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            for (var i = 0; i < layers.Length; i++)
                array.SetPixels32(layers[i].GetPixels32(), i);
            array.Apply(false, false);
            AssetDatabase.CreateAsset(array, $"{GeneratedRoot}/Textures/{name}.asset");
            return array;
        }

        static Mesh SaveMesh(Mesh mesh)
        {
            AssetDatabase.CreateAsset(mesh, $"{GeneratedRoot}/Meshes/{mesh.name}.asset");
            return mesh;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Локация — первая сцена в списке сборки.</summary>
        static void SetMainScene(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path || !File.Exists(s.path));
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
