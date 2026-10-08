using ChroniclesOfRebirth.Gameplay.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChroniclesOfRebirth.Gameplay.CameraSystem
{
    /// <summary>
    /// Кинематографичная камера HD-2D. Следует за целью с мягким запаздыванием, опережает её по направлению
    /// движения, слегка поворачивается при боковом движении и едва заметно «дышит».
    /// Приближение (zoom) одновременно меняет дистанцию и наклон: ближе — ниже, дальше — выше.
    /// Колесо мыши меняет приближение (для подбора значений на тестах).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 targetOffset = new(0f, 0.8f, 0f);
        [SerializeField] float yaw;
        [SerializeField] float fieldOfView = 26f;
        [SerializeField] float followSharpness = 4f;

        [Header("Приближение: 0 — ближе всего, 1 — дальше всего")]
        [SerializeField, Range(0f, 1f)] float zoom = 0.6f;
        [SerializeField] float nearDistance = 12f;
        [SerializeField] float farDistance = 26f;
        [SerializeField] float nearPitch = 24f;
        [SerializeField] float farPitch = 40f;
        [SerializeField] float zoomStep = 0.08f;
        [SerializeField] float zoomSharpness = 8f;

        [Header("Кинематографичность")]
        [Tooltip("На сколько юнитов камера опережает героя по направлению движения.")]
        [SerializeField] float lookAhead = 1.6f;
        [SerializeField] float lookAheadSharpness = 2.5f;
        [Tooltip("Наибольший поворот камеры (в градусах) при боковом движении героя.")]
        [SerializeField] float lateralYaw = 2.5f;
        [SerializeField] float yawSharpness = 2f;
        [SerializeField] float breathAmplitude = 0.04f;
        [SerializeField] float breathAngle = 0.12f;
        [SerializeField] float breathSpeed = 0.35f;

        [Header("Под кронами: в чаще камера опускается ниже крон и подходит ближе")]
        [SerializeField] float canopyDistance = 9f;
        [SerializeField] float canopyPitch = 14f;
        [Tooltip("Густота леса, с которой камера начинает опускаться, и густота, при которой она полностью под кронами. " +
                 "Диапазон узкий: промежуточная высота — как раз уровень крон, камера проходит её только в переезде.")]
        [SerializeField] Vector2 canopyDensityRange = new(0.55f, 0.65f);
        [SerializeField] float canopySharpness = 1.5f;
        [Tooltip("Наибольшая высота камеры над землёй под ней: выше начинаются кроны. Рельеф ступенчатый, поэтому " +
                 "камера подходит ближе к герою, пока не окажется ниже крон.")]
        [SerializeField] float canopyMaxClearance = 3f;
        [SerializeField] float canopyMinDistance = 5f;

        [Header("Растворение листвы у камеры (доля дистанции до героя)")]
        [SerializeField] float leafFadeStart = 0.45f;
        [SerializeField] float leafFadeEnd = 0.2f;

        [Tooltip("Границы точки фокуса (minX, minZ, maxX, maxZ), чтобы камера не показывала край мира.")]
        [SerializeField] bool clampFocus;
        [SerializeField] Vector4 focusBounds;

        static readonly int CameraFadeId = Shader.PropertyToID("_CoR_CameraFade");

        Camera _camera;
        CanopyFadeController _canopy;
        float _canopyBlend;
        float _canopyFitDistance;
        Vector3 _focus;
        Vector3 _lastTargetPosition;
        Vector3 _lookAheadOffset;
        float _yawOffset;
        float _currentZoom;

        public float Distance => Mathf.Lerp(Mathf.Lerp(nearDistance, farDistance, _currentZoom), _canopyFitDistance, _canopyBlend);
        public float Pitch => Mathf.Lerp(Mathf.Lerp(nearPitch, farPitch, _currentZoom), canopyPitch, _canopyBlend);

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                SnapToTarget();
            }
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.fieldOfView = fieldOfView;
            _currentZoom = zoom;
        }

        void OnEnable() => SnapToTarget();

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (target == null || dt <= 0f)
                return;

            ReadZoomInput();
            UpdateMotionResponse(dt);

            _currentZoom = Mathf.Lerp(_currentZoom, zoom, 1f - Mathf.Exp(-zoomSharpness * dt));
            _canopyBlend = Mathf.Lerp(_canopyBlend, CanopyTarget(), 1f - Mathf.Exp(-canopySharpness * dt));
            _focus = Vector3.Lerp(_focus, DesiredFocus(), 1f - Mathf.Exp(-followSharpness * dt));
            if (_canopyBlend > 0.01f)
                _canopyFitDistance = Mathf.Lerp(_canopyFitDistance, FitCanopyDistance(), 1f - Mathf.Exp(-zoomSharpness * 0.5f * dt));
            Apply(true);
        }

        /// <summary>
        /// Дистанция под кронами с учётом ступенчатого рельефа: камера подходит ближе к герою,
        /// пока её высота над землёй под ней не станет меньше <see cref="canopyMaxClearance"/>.
        /// </summary>
        float FitCanopyDistance()
        {
            var rotation = Quaternion.Euler(canopyPitch, yaw + _yawOffset, 0f);
            var back = rotation * Vector3.back;
            for (var d = canopyDistance; d > canopyMinDistance; d -= 0.5f)
                if (GroundClearance(_focus + back * d) <= canopyMaxClearance)
                    return d;
            return canopyMinDistance;
        }

        static float GroundClearance(Vector3 position)
        {
            const float probe = 30f;
            return Physics.Raycast(position + Vector3.up * probe, Vector3.down, out var hit, probe * 2f, ~0, QueryTriggerInteraction.Ignore)
                ? position.y - hit.point.y
                : 0f;
        }

        /// <summary>Насколько камера должна быть под кронами (0..1) по густоте леса вокруг героя.</summary>
        float CanopyTarget()
        {
            if (_canopy == null && target != null)
                _canopy = target.GetComponent<CanopyFadeController>();
            if (_canopy == null)
                return 0f;
            var t = Mathf.InverseLerp(canopyDensityRange.x, canopyDensityRange.y, _canopy.Density);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Опережение и боковой поворот по скорости цели.</summary>
        void UpdateMotionResponse(float dt)
        {
            var position = target.position;
            var velocity = (position - _lastTargetPosition) / dt;
            _lastTargetPosition = position;
            velocity.y = 0f;

            var speed = velocity.magnitude;
            if (speed > 30f) // телепорт — не реагируем
                velocity = Vector3.zero;

            var ahead = speed > 0.2f ? velocity.normalized * (lookAhead * Mathf.Clamp01(speed / 3f)) : Vector3.zero;
            _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, ahead, 1f - Mathf.Exp(-lookAheadSharpness * dt));

            var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var lateral = Mathf.Clamp(Vector3.Dot(velocity, right) / 3f, -1f, 1f);
            _yawOffset = Mathf.Lerp(_yawOffset, lateral * lateralYaw, 1f - Mathf.Exp(-yawSharpness * dt));
        }

        void ReadZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f))
                return;

            zoom = Mathf.Clamp01(zoom - Mathf.Sign(scroll) * zoomStep);
            Debug.Log($"[CameraRig] Приближение {zoom:0.00}: дистанция {Mathf.Lerp(nearDistance, farDistance, zoom):0.0}, " +
                      $"наклон {Mathf.Lerp(nearPitch, farPitch, zoom):0.0}°");
        }

        public void SnapToTarget()
        {
            if (target == null)
                return;
            _currentZoom = zoom;
            _canopyBlend = CanopyTarget();
            _lastTargetPosition = target.position;
            _focus = DesiredFocus();
            _canopyFitDistance = Application.isPlaying ? FitCanopyDistance() : canopyDistance;
            _lookAheadOffset = Vector3.zero;
            _yawOffset = 0f;
            _focus = DesiredFocus();
            Apply(false);
        }

        Vector3 DesiredFocus()
        {
            var focus = target.position + targetOffset + _lookAheadOffset;
            if (clampFocus)
            {
                focus.x = Mathf.Clamp(focus.x, focusBounds.x, focusBounds.z);
                focus.z = Mathf.Clamp(focus.z, focusBounds.y, focusBounds.w);
            }
            return focus;
        }

        void Apply(bool breathe)
        {
            var pitch = Pitch;
            var yawAngle = yaw + _yawOffset;
            var offset = Vector3.zero;

            if (breathe)
            {
                // Медленный шум Перлина: ощущение ручной камеры без тряски.
                var t = Time.time * breathSpeed;
                pitch += (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * breathAngle;
                yawAngle += (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * breathAngle;
                offset = new Vector3(Mathf.PerlinNoise(t, 5.1f) - 0.5f, Mathf.PerlinNoise(9.2f, t) - 0.5f, 0f) * (2f * breathAmplitude);
            }

            var rotation = Quaternion.Euler(pitch, yawAngle, 0f);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * Distance + rotation * offset, rotation);

            // Листва ближе этой доли дистанции до героя растворяется (PixelSprite.shader, CameraFade).
            Shader.SetGlobalVector(CameraFadeId, new Vector4(Distance * leafFadeStart, Distance * leafFadeEnd, 0f, 0f));
        }

        void OnValidate()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();
            if (_camera != null)
                _camera.fieldOfView = fieldOfView;
            SnapToTarget();
        }
    }
}
