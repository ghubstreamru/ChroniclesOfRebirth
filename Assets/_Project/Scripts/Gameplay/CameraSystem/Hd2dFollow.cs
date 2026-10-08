using ChroniclesOfRebirth.Gameplay.World;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChroniclesOfRebirth.Gameplay.CameraSystem
{
    /// <summary>
    /// Кинематографичная камера HD-2D как компонент Cinemachine (стадия Body: задаёт и положение, и поворот).
    /// Следует за целью (Tracking Target камеры) с мягким запаздыванием, опережает её по направлению движения,
    /// слегка поворачивается при боковом движении и едва заметно «дышит». Приближение меняет дистанцию и наклон.
    /// В чаще уходит под кроны. Несколько таких камер с разными профилями — это зоны камеры: переезды между
    /// ними плавно смешивает CinemachineBrain.
    /// </summary>
    [AddComponentMenu("Chronicles of Rebirth/Camera/HD-2D Follow")]
    [CameraPipeline(CinemachineCore.Stage.Body)]
    public sealed class Hd2dFollow : CinemachineComponentBase
    {
        [SerializeField] Vector3 targetOffset = new(0f, 0.8f, 0f);
        [SerializeField] float yaw;
        [SerializeField] float followSharpness = 4f;

        [Header("Приближение: 0 — ближе всего, 1 — дальше всего")]
        [SerializeField, Range(0f, 1f)] float zoom = 0.6f;
        [SerializeField] float nearDistance = 12f;
        [SerializeField] float farDistance = 26f;
        [SerializeField] float nearPitch = 24f;
        [SerializeField] float farPitch = 40f;
        [Tooltip("Колесо мыши меняет приближение (для подбора значений на тестах).")]
        [SerializeField] bool mouseZoom = true;
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

        [Header("Под кронами: кроны закрыли героя — камера опускается ниже крон и подходит ближе")]
        [SerializeField] bool canopyMode = true;
        [Tooltip("Датчики крон (триггеры). Камера уходит под кроны, когда они закрывают героя из обычного ракурса. " +
                 "Пусто — запасной вариант: по густоте леса вокруг героя.")]
        [SerializeField] LayerMask canopyOccluders;
        [Tooltip("Сколько секунд кроны должны закрывать героя, чтобы камера ушла под них (прошёл под одним деревом — не ныряем).")]
        [SerializeField] float canopyEnterDelay = 0.35f;
        [Tooltip("Сколько секунд обзор сверху должен быть свободен, чтобы камера вернулась наверх.")]
        [SerializeField] float canopyExitDelay = 0.8f;
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

        [Header("Обход рельефа: склон закрыл героя — камера наклоняется круче и заглядывает сверху")]
        [Tooltip("Слои, которые закрывают обзор (рельеф). Стволы и камни сюда не входят — их показывает «призрак» героя.")]
        [SerializeField] LayerMask occluders;
        [SerializeField] float maxPitch = 72f;
        [SerializeField] float liftSharpness = 4f;
        [SerializeField] float lowerSharpness = 1.2f;
        [Tooltip("Сколько секунд обзор должен быть свободен, прежде чем камера начнёт опускаться обратно.")]
        [SerializeField] float lowerDelay = 0.6f;
        [SerializeField] float occludedMinDistance = 7f;
        [Tooltip("Свободное место вокруг камеры: ближе к склону камера не встаёт (иначе склон заслоняет кадр у самого объектива).")]
        [SerializeField] float cameraClearance = 2f;

        [Header("Край обрыва: впереди перепад выше прыжка — камера показывает, куда падать")]
        [Tooltip("Перепад (юниты), с которого край считается обрывом: выше прыжка героя.")]
        [SerializeField] float edgeDrop = 1.3f;
        [Tooltip("Как далеко впереди героя ищется край (юниты).")]
        [SerializeField] float edgeProbe = 2.5f;
        [SerializeField] float edgeExtraPitch = 14f;
        [Tooltip("На сколько центр кадра сдвигается к краю, чтобы нижний уровень попал в кадр.")]
        [SerializeField] float edgeLookAhead = 2.5f;
        [SerializeField] float edgeSharpness = 3f;

        [Header("Растворение листвы у камеры (доля дистанции до героя)")]
        [SerializeField] float leafFadeStart = 0.45f;
        [SerializeField] float leafFadeEnd = 0.2f;

        [Tooltip("Границы точки фокуса (minX, minZ, maxX, maxZ), чтобы камера не показывала край мира.")]
        [SerializeField] bool clampFocus;
        [SerializeField] Vector4 focusBounds;

        static readonly int CameraFadeId = Shader.PropertyToID("_CoR_CameraFade");

        CanopyFadeController _canopy;
        Transform _canopyOwner;
        bool _initialized;
        float _canopyBlend;
        float _canopyFitDistance;
        Vector3 _focus;
        Vector3 _lastTargetPosition;
        Vector3 _lookAheadOffset;
        float _yawOffset;
        float _currentZoom;
        float _lift;
        float _pull = 1f;
        float _clearTime;
        bool _underCanopy;
        float _canopyTimer;
        float _edge;
        Vector3 _moveDirection = Vector3.forward;

        float BaseDistance => Mathf.Lerp(Mathf.Lerp(nearDistance, farDistance, _currentZoom), _canopyFitDistance, _canopyBlend);
        float BasePitch => Mathf.Lerp(Mathf.Lerp(nearPitch, farPitch, _currentZoom), canopyPitch, _canopyBlend);

        public float Distance => Mathf.Lerp(Mathf.Min(occludedMinDistance, BaseDistance), BaseDistance, _pull);
        public float Pitch => Mathf.Min(BasePitch + _lift + _edge * edgeExtraPitch, maxPitch);

        public override bool IsValid => enabled && FollowTarget != null;
        public override CinemachineCore.Stage Stage => CinemachineCore.Stage.Body;

        /// <summary>Постоянный ракурс зоны: без зума, заданные наклон и дистанция.</summary>
        public void ConfigureFixed(float pitch, float distance)
        {
            nearPitch = farPitch = pitch;
            nearDistance = farDistance = distance;
            mouseZoom = false;
        }

        public void Configure(Vector4 bounds, LayerMask terrainMask, LayerMask canopyMask)
        {
            clampFocus = true;
            focusBounds = bounds;
            occluders = terrainMask;
            canopyOccluders = canopyMask;
        }

        public override void MutateCameraState(ref CameraState state, float deltaTime)
        {
            var target = FollowTarget;
            if (target == null)
                return;

            if (!_initialized || deltaTime < 0f)
                Snap(target);
            else if (deltaTime > 0f)
                Step(target, deltaTime);

            var (position, rotation) = Pose(breathe: _initialized && deltaTime >= 0f);
            state.RawPosition = position;
            state.RawOrientation = rotation;

            // Листва ближе этой доли дистанции до героя растворяется (PixelSprite.shader, CameraFade).
            if (CinemachineCore.IsLive(VirtualCamera))
                Shader.SetGlobalVector(CameraFadeId, new Vector4(Distance * leafFadeStart, Distance * leafFadeEnd, 0f, 0f));
        }

        public override void OnTargetObjectWarped(Transform target, Vector3 positionDelta)
        {
            if (target != FollowTarget)
                return;
            _focus += positionDelta;
            _lastTargetPosition += positionDelta;
        }

        void Step(Transform target, float dt)
        {
            if (mouseZoom)
                ReadZoomInput();
            UpdateMotionResponse(target, dt);

            _currentZoom = Mathf.Lerp(_currentZoom, zoom, 1f - Mathf.Exp(-zoomSharpness * dt));
            _canopyBlend = Mathf.Lerp(_canopyBlend, CanopyTarget(target, dt), 1f - Mathf.Exp(-canopySharpness * dt));
            _focus = Vector3.Lerp(_focus, DesiredFocus(target), 1f - Mathf.Exp(-followSharpness * dt));
            if (_canopyBlend > 0.01f)
                _canopyFitDistance = Mathf.Lerp(_canopyFitDistance, FitCanopyDistance(), 1f - Mathf.Exp(-zoomSharpness * 0.5f * dt));
            _edge = Mathf.Lerp(_edge, EdgeAhead(target) ? 1f : 0f, 1f - Mathf.Exp(-edgeSharpness * dt));
            UpdateOcclusion(target, dt);
        }

        /// <summary>Есть ли впереди по направлению движения перепад выше прыжка (по рельефу).</summary>
        bool EdgeAhead(Transform target)
        {
            if (occluders.value == 0 || !GroundHeight(target.position, out var here))
                return false;
            for (var d = edgeProbe / 3f; d <= edgeProbe + 0.01f; d += edgeProbe / 3f)
                if (GroundHeight(target.position + _moveDirection * d, out var there) && here - there > edgeDrop)
                    return true;
            return false;
        }

        bool GroundHeight(Vector3 position, out float height)
        {
            const float probe = 3f;
            var hit = Physics.Raycast(position + Vector3.up * probe, Vector3.down, out var info, probe + 40f, occluders,
                QueryTriggerInteraction.Ignore);
            height = hit ? info.point.y : 0f;
            return hit;
        }

        /// <summary>
        /// Склон между камерой и героем: ищем наименьший дополнительный наклон, при котором герой виден,
        /// и плавно наклоняемся до него. Если не помогает и вид сверху — подходим ближе.
        /// Назад камера опускается медленнее и только после паузы со свободным обзором — без раскачки.
        /// </summary>
        void UpdateOcclusion(Transform target, float dt)
        {
            if (occluders.value == 0)
                return;

            var basePitch = BasePitch;
            var liftTarget = maxPitch - basePitch;
            for (var extra = 0f; basePitch + extra < maxPitch; extra += 4f)
            {
                if (!Visible(target, basePitch + extra, BaseDistance))
                    continue;
                liftTarget = extra;
                break;
            }
            var pullTarget = liftTarget < maxPitch - basePitch || Visible(target, maxPitch, BaseDistance) ? 1f : 0f;

            if (liftTarget >= _lift - 0.5f || pullTarget < _pull)
            {
                _clearTime = 0f;
                _lift = Mathf.Lerp(_lift, Mathf.Max(liftTarget, _lift), 1f - Mathf.Exp(-liftSharpness * dt));
                _pull = Mathf.Lerp(_pull, Mathf.Min(pullTarget, _pull), 1f - Mathf.Exp(-liftSharpness * dt));
                return;
            }

            _clearTime += dt;
            if (_clearTime < lowerDelay)
                return;
            _lift = Mathf.Lerp(_lift, liftTarget, 1f - Mathf.Exp(-lowerSharpness * dt));
            _pull = Mathf.Lerp(_pull, pullTarget, 1f - Mathf.Exp(-lowerSharpness * dt));
        }

        /// <summary>Видны ли ноги и голова героя из камеры с таким наклоном и дистанцией.</summary>
        bool Visible(Transform target, float pitch, float distance)
        {
            var rotation = Quaternion.Euler(pitch, yaw + _yawOffset, 0f);
            var camera = _focus - rotation * Vector3.forward * distance;
            return !Physics.CheckSphere(camera, cameraClearance, occluders, QueryTriggerInteraction.Ignore) &&
                   Clear(target.position + Vector3.up * 0.3f, camera) && Clear(target.position + Vector3.up * 1.3f, camera);
        }

        bool Clear(Vector3 point, Vector3 camera)
        {
            // Начинаем чуть в стороне от героя, чтобы не задеть землю у него под ногами.
            var start = point + (camera - point).normalized * 0.5f;
            return !Physics.Linecast(start, camera, occluders, QueryTriggerInteraction.Ignore);
        }

        void Snap(Transform target)
        {
            _currentZoom = zoom;
            _edge = 0f;
            _focus = DesiredFocus(target);
            _underCanopy = false;
            _canopyTimer = 0f;
            _canopyBlend = CanopyTarget(target, -1f);
            _lastTargetPosition = target.position;
            _lookAheadOffset = Vector3.zero;
            _yawOffset = 0f;
            _focus = DesiredFocus(target);
            _canopyFitDistance = Application.isPlaying ? FitCanopyDistance() : canopyDistance;
            _lift = 0f;
            _pull = 1f;
            _clearTime = 0f;
            _initialized = true;
        }

        (Vector3 position, Quaternion rotation) Pose(bool breathe)
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
            return (_focus - rotation * Vector3.forward * Distance + rotation * offset, rotation);
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

        /// <summary>
        /// Насколько камера должна быть под кронами (0..1). Основной способ — закрывают ли кроны героя из обычного
        /// (верхнего) ракурса. Проверяем всегда верхний ракурс, а не текущий: снизу кроны героя не закрывают,
        /// и иначе камера качалась бы вверх-вниз. dt &lt; 0 — решить сразу (телепорт, загрузка).
        /// </summary>
        float CanopyTarget(Transform target, float dt)
        {
            if (!canopyMode)
                return 0f;
            if (canopyOccluders.value == 0)
                return DensityCanopyTarget(target);

            var occluded = CrownsHideTarget(target);
            if (dt < 0f)
            {
                _underCanopy = occluded;
                _canopyTimer = 0f;
            }
            else if (occluded != _underCanopy)
            {
                _canopyTimer += dt;
                if (_canopyTimer >= (occluded ? canopyEnterDelay : canopyExitDelay))
                {
                    _underCanopy = occluded;
                    _canopyTimer = 0f;
                }
            }
            else
            {
                _canopyTimer = 0f;
            }
            return _underCanopy ? 1f : 0f;
        }

        /// <summary>Кроны (датчики-триггеры) между героем и камерой в обычном верхнем ракурсе.</summary>
        bool CrownsHideTarget(Transform target)
        {
            var rotation = Quaternion.Euler(Mathf.Lerp(nearPitch, farPitch, _currentZoom), yaw + _yawOffset, 0f);
            var camera = _focus - rotation * Vector3.forward * Mathf.Lerp(nearDistance, farDistance, _currentZoom);
            var chest = target.position + Vector3.up * 0.9f;
            return Physics.Linecast(chest, camera, canopyOccluders, QueryTriggerInteraction.Collide);
        }

        /// <summary>Запасной способ: по густоте леса вокруг героя (карта плотности).</summary>
        float DensityCanopyTarget(Transform target)
        {
            if (_canopyOwner != target)
            {
                _canopyOwner = target;
                _canopy = target.GetComponent<CanopyFadeController>();
            }
            if (_canopy == null)
                return 0f;
            var t = Mathf.InverseLerp(canopyDensityRange.x, canopyDensityRange.y, _canopy.Density);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Опережение и боковой поворот по скорости цели.</summary>
        void UpdateMotionResponse(Transform target, float dt)
        {
            var position = target.position;
            var velocity = (position - _lastTargetPosition) / dt;
            _lastTargetPosition = position;
            velocity.y = 0f;

            var speed = velocity.magnitude;
            if (speed > 30f) // телепорт — не реагируем
                velocity = Vector3.zero;

            if (speed > 0.2f)
                _moveDirection = velocity / speed; // у края стоим — помним, куда шли
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
            Debug.Log($"[Hd2dFollow] Приближение {zoom:0.00}: дистанция {Mathf.Lerp(nearDistance, farDistance, zoom):0.0}, " +
                      $"наклон {Mathf.Lerp(nearPitch, farPitch, zoom):0.0}°");
        }

        Vector3 DesiredFocus(Transform target)
        {
            var focus = target.position + targetOffset + _lookAheadOffset + _moveDirection * (edgeLookAhead * _edge);
            if (clampFocus)
            {
                focus.x = Mathf.Clamp(focus.x, focusBounds.x, focusBounds.z);
                focus.z = Mathf.Clamp(focus.z, focusBounds.y, focusBounds.w);
            }
            return focus;
        }

        /// <summary>Сразу встать на место — при телепорте героя, загрузке, смене цели.</summary>
        public void SnapNow() => _initialized = false;

        void OnValidate() => _initialized = false;
    }
}
