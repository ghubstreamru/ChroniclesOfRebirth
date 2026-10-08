using Unity.Cinemachine;
using UnityEngine;

namespace ChroniclesOfRebirth.Gameplay.CameraSystem
{
    /// <summary>
    /// Зона камеры: пока герой внутри круга, включена своя виртуальная камера с заданным ракурсом.
    /// Переезд между ракурсами плавно делает CinemachineBrain; при перекрытии зон побеждает более высокий приоритет.
    /// Выход из зоны — с запасом, чтобы на границе камера не переключалась туда-сюда.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class CameraZone : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector2 center;
        [SerializeField] float radius = 8f;
        [SerializeField] float exitMargin = 1.5f;

        CinemachineCamera _camera;
        Hd2dFollow _follow;
        bool _inside;

        public void Setup(Transform zoneTarget, Vector2 zoneCenter, float zoneRadius)
        {
            target = zoneTarget;
            center = zoneCenter;
            radius = zoneRadius;
        }

        void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
            _follow = GetComponent<Hd2dFollow>();
            _camera.enabled = false;
        }

        void Update()
        {
            if (target == null)
                return;

            var p = target.position;
            var distance = Vector2.Distance(new Vector2(p.x, p.z), center);
            var inside = _inside ? distance < radius + exitMargin : distance < radius;
            if (inside == _inside)
                return;

            _inside = inside;
            if (inside && _follow != null)
                _follow.SnapNow(); // ракурс зоны считается от текущего положения героя, переезд к нему — плавный
            _camera.enabled = inside;
        }

        void OnDrawGizmosSelected()
        {
            var y = target != null ? target.position.y : transform.position.y;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            const int segments = 48;
            for (var i = 0; i < segments; i++)
            {
                var a = i / (float)segments * Mathf.PI * 2f;
                var b = (i + 1) / (float)segments * Mathf.PI * 2f;
                Gizmos.DrawLine(new Vector3(center.x + Mathf.Cos(a) * radius, y, center.y + Mathf.Sin(a) * radius),
                    new Vector3(center.x + Mathf.Cos(b) * radius, y, center.y + Mathf.Sin(b) * radius));
            }
        }
    }
}
