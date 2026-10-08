using UnityEngine;

namespace ChroniclesOfRebirth.Gameplay.World
{
    /// <summary>Медленное «дыхание» магического света: синусоида плюс шум, чтобы пульс не был механическим.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class MagicPulse : MonoBehaviour
    {
        [SerializeField] float amplitude = 0.35f;
        [SerializeField] float period = 4.5f;
        [SerializeField] float noise = 0.15f;

        Light _light;
        float _base;
        float _offset;

        void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            _offset = Random.value * 10f;
        }

        void Update()
        {
            var t = Time.time + _offset;
            var wave = Mathf.Sin(t * Mathf.PI * 2f / period);
            var drift = (Mathf.PerlinNoise(t * 0.6f, _offset) - 0.5f) * 2f;
            _light.intensity = _base * (1f + amplitude * wave + noise * drift);
        }
    }
}
