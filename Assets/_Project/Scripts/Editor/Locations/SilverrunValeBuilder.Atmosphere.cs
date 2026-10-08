using ChroniclesOfRebirth.Core.Pixel;
using ChroniclesOfRebirth.Gameplay.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChroniclesOfRebirth.Editor.Locations
{
    /// <summary>
    /// «Магия в воздухе» — тонко: редкие тусклые пылинки вокруг героя и загадочное дыхание Дуба-Прадеда
    /// (несколько медленных огоньков, мягкий бирюзовый свет под кроной). Главную атмосферу создаёт свет
    /// (объёмные лучи из теней крон, дымка, цвет) — см. Docs/Design/PolishPlan.md, этап 1.
    /// </summary>
    public static partial class SilverrunValeBuilder
    {
        const string ParticlesShader = "Universal Render Pipeline/Particles/Unlit";

        static readonly Color MoteGold = new(1f, 0.8f, 0.42f);
        static readonly Color MoteCyan = new(0.45f, 0.95f, 1f);
        static readonly Color OakGlow = new(0.4f, 0.92f, 0.95f);

        static void BuildAtmosphere(SilverrunValeLayout layout, GameObject hero, Transform parent)
        {
            var root = new GameObject("Atmosphere").transform;
            root.SetParent(parent, false);

            var mote = SaveTexture(MagicTextures.Mote());
            var moteMaterial = SaveMaterial(AdditiveMaterial("Magic_Motes", mote, 1.2f));
            var spiritMaterial = SaveMaterial(AdditiveMaterial("Magic_Spirits", mote, 1.8f));

            BuildAirMotes(hero.transform, moteMaterial);
            BuildOakMagic(layout, root, spiritMaterial);
        }

        /// <summary>Аддитивный материал частиц URP; intensity &gt; 1 — HDR-свечение, которое подхватывает bloom.</summary>
        static Material AdditiveMaterial(string name, Texture2D texture, float intensity)
        {
            var material = new Material(Shader.Find(ParticlesShader)) { name = name };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(intensity, intensity, intensity, 1f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        // ---------- Пылинки в воздухе ----------

        /// <summary>
        /// Золотые и бирюзовые пылинки в объёме вокруг героя. Симуляция в мире: герой идёт сквозь них,
        /// а новые появляются впереди — воздух всюду «живой».
        /// </summary>
        static void BuildAirMotes(Transform hero, Material material)
        {
            var go = new GameObject("Air_Motes");
            go.transform.SetParent(hero, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 4f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.startColor = RandomColors((MoteGold, 0.85f), (MoteCyan, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.004f;
            main.maxParticles = 160;

            var emission = ps.emission;
            emission.rateOverTime = 13f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 5f, 28f);

            AddDrift(ps, 0.35f, 0.22f);
            AddTwinkle(ps);
            SetupRenderer(go, material);
        }

        // ---------- Дуб-Прадед ----------

        static void BuildOakMagic(SilverrunValeLayout layout, Transform parent, Material spirits)
        {
            var oak = SilverrunValeLayout.GiantOakPosition;
            var ground = new Vector3(oak.x, layout.Field.SampleHeight(oak.x, oak.y), oak.y);
            var root = new GameObject("Grandfather_Oak_Magic").transform;
            root.SetParent(parent, false);
            root.position = ground;

            // Мягкое бирюзовое дыхание под кроной и тёплое у корней — едва заметно, вразнобой.
            AddPulsingLight(root, "Crown_Glow", ground + Vector3.up * 8.5f, OakGlow, 2.2f, 15f, 7f);
            AddPulsingLight(root, "Root_Glow", ground + Vector3.up * 1.2f, new Color(1f, 0.78f, 0.45f), 1.2f, 7f, 4.3f);

            // Огоньки-духи: поднимаются от корней в крону и медленно кружат в ней.
            var rising = CreateSpirits(root, "Spirits_Rising", ground + Vector3.up * 0.4f, spirits);
            var shape = rising.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 5.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var velocity = rising.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // Все кривые модуля должны быть в одном режиме (две константы), иначе Unity их отбрасывает.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);

            var crown = CreateSpirits(root, "Spirits_Crown", ground + Vector3.up * 10f, spirits);
            var crownShape = crown.shape;
            crownShape.shapeType = ParticleSystemShapeType.Sphere;
            crownShape.radius = 8f;
            crownShape.scale = new Vector3(1f, 0.55f, 1f);
            var crownMain = crown.main;
            var crownEmission = crown.emission;
            crownEmission.rateOverTime = 1.5f;
        }

        static ParticleSystem CreateSpirits(Transform parent, string name, Vector3 position, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
            main.startColor = RandomColors((MoteCyan, 0.7f), (MoteGold, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            // Несколько медленных огоньков, а не рой.
            var emission = ps.emission;
            emission.rateOverTime = 1.5f;

            AddDrift(ps, 0.4f, 0.3f);
            AddTwinkle(ps);
            SetupRenderer(go, material);
            return ps;
        }

        static void AddPulsingLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range, float period)
        {
            AddPointLight(parent, name, position, color, intensity, range);
            var pulse = parent.Find(name).gameObject.AddComponent<MagicPulse>();
            var so = new UnityEditor.SerializedObject(pulse);
            so.FindProperty("period").floatValue = period;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Общие модули частиц ----------

        /// <summary>Случайный цвет из набора: (цвет, верхняя граница доли в [0, 1]).</summary>
        static ParticleSystem.MinMaxGradient RandomColors(params (Color color, float upTo)[] colors)
        {
            var keys = new GradientColorKey[colors.Length * 2];
            var from = 0f;
            for (var k = 0; k < colors.Length; k++)
            {
                keys[k * 2] = new GradientColorKey(colors[k].color, from);
                keys[k * 2 + 1] = new GradientColorKey(colors[k].color, Mathf.Max(from, colors[k].upTo - 0.001f));
                from = colors[k].upTo;
            }

            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }

        /// <summary>Медленный дрейф по шуму — пылинки «плавают», а не летят по прямой.</summary>
        static void AddDrift(ParticleSystem ps, float strength, float frequency)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.15f;
            noise.damping = true;
        }

        /// <summary>Появление, мерцание размером и угасание.</summary>
        static void AddTwinkle(ParticleSystem ps)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(0.3f, 0.55f), new Keyframe(0.45f, 1f),
                new Keyframe(0.6f, 0.6f), new Keyframe(0.78f, 1f), new Keyframe(1f, 0.2f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        static void SetupRenderer(GameObject go, Material material)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 0.08f;
        }
    }
}
