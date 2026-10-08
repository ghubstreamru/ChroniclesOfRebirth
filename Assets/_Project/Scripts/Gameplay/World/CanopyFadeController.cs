using UnityEngine;

namespace ChroniclesOfRebirth.Gameplay.World
{
    /// <summary>
    /// Сообщает шейдеру листвы позицию героя и радиус бледнения крон.
    /// Радиус зависит от густоты леса вокруг (карта плотности, запечённая из планировки):
    /// на открытом месте бледнеют только кроны, закрывающие героя, в чаще — все кроны вокруг.
    /// Работает и в редакторе — при расстановке уровня видно, как поведут себя кроны.
    /// </summary>
    [ExecuteAlways]
    public sealed class CanopyFadeController : MonoBehaviour
    {
        static readonly int PlayerPositionId = Shader.PropertyToID("_CoR_PlayerPosition");
        static readonly int CanopyRadiusId = Shader.PropertyToID("_CoR_CanopyRadius");
        static readonly int OccludeMarginId = Shader.PropertyToID("_CoR_OccludeMargin");

        [SerializeField] Vector3 offset = new(0f, 0.75f, 0f);

        [Header("Карта плотности леса (0 — открыто, 1 — чаща)")]
        [SerializeField] Texture2D densityMap;
        [Tooltip("Мировая область карты: minX, minZ, ширина, глубина.")]
        [SerializeField] Vector4 mapBounds;

        [Header("Радиусы")]
        [Tooltip("Горизонтальный радиус бледнения на открытом месте (юниты).")]
        [SerializeField] float openRadius = 0.5f;
        [Tooltip("Горизонтальный радиус бледнения в чаще (юниты).")]
        [SerializeField] float denseRadius = 5f;
        [Tooltip("Запас вокруг линии «камера → герой» (юниты): крона бледнеет, если её край ближе к линии, чем этот запас.")]
        [SerializeField] float occludeMargin = 0.8f;
        [SerializeField] float densitySharpness = 3f;

        float _density;

        /// <summary>Сглаженная густота леса вокруг героя: 0 — открыто, 1 — чаща. По ней камера уходит под кроны.</summary>
        public float Density => _density;

        public void Setup(Texture2D map, Vector4 bounds)
        {
            densityMap = map;
            mapBounds = bounds;
        }

        void OnEnable() => _density = SampleDensity();

        void LateUpdate()
        {
            _density = Application.isPlaying
                ? Mathf.Lerp(_density, SampleDensity(), 1f - Mathf.Exp(-densitySharpness * Time.deltaTime))
                : SampleDensity();

            var p = transform.position + offset;
            Shader.SetGlobalVector(PlayerPositionId, new Vector4(p.x, p.y, p.z, 1f));
            Shader.SetGlobalFloat(CanopyRadiusId, Mathf.Lerp(openRadius, denseRadius, _density));
            Shader.SetGlobalFloat(OccludeMarginId, occludeMargin);
        }

        void OnDisable() => Shader.SetGlobalVector(PlayerPositionId, Vector4.zero);

        float SampleDensity()
        {
            if (densityMap == null || mapBounds.z <= 0f || mapBounds.w <= 0f)
                return 0f;
            var u = (transform.position.x - mapBounds.x) / mapBounds.z;
            var v = (transform.position.z - mapBounds.y) / mapBounds.w;
            return densityMap.GetPixelBilinear(u, v).r;
        }
    }
}
