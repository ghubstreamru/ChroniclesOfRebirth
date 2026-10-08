using UnityEngine;
using UnityEngine.InputSystem;

namespace ChroniclesOfRebirth.Gameplay.Player
{
    /// <summary>
    /// Передвижение героя относительно камеры. Ввод — общий файл действий проекта (Player/Move, Player/Sprint, Player/Jump).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] float walkSpeed = 3.2f;
        [SerializeField] float runSpeed = 5.6f;
        [SerializeField] float gravity = -20f;
        [Tooltip("Высота прыжка в юнитах: 1,1 — запрыгнуть на два уступа рельефа (по 0,5).")]
        [SerializeField] float jumpHeight = 1.1f;
        [Tooltip("Сколько секунд после схода с края ещё можно прыгнуть.")]
        [SerializeField] float coyoteTime = 0.12f;
        [Tooltip("Сколько секунд помнится нажатие прыжка до приземления.")]
        [SerializeField] float jumpBuffer = 0.12f;
        [SerializeField] Transform cameraTransform;

        CharacterController _controller;
        InputAction _move;
        InputAction _sprint;
        InputAction _jump;
        float _verticalVelocity;
        float _sinceGrounded;
        float _sinceJumpPressed = float.MaxValue;

        /// <summary>Горизонтальная скорость за последний кадр (для анимации).</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>Герой в воздухе (прыжок или падение с уступа).</summary>
        public bool Airborne { get; private set; }

        public float WalkSpeed => walkSpeed;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _move = InputSystem.actions?.FindAction("Player/Move");
            _sprint = InputSystem.actions?.FindAction("Player/Sprint");
            _jump = InputSystem.actions?.FindAction("Player/Jump");
            if (_move == null)
                Debug.LogError("[PlayerController] Не найдено действие Player/Move в общем файле действий проекта.");
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        void OnEnable()
        {
            _move?.Enable();
            _sprint?.Enable();
            _jump?.Enable();
        }

        void Update()
        {
            var input = _move?.ReadValue<Vector2>() ?? Vector2.zero;

            var forward = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);

            var direction = right * input.x + forward * input.y;
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            var speed = _sprint != null && _sprint.IsPressed() ? runSpeed : walkSpeed;
            var horizontal = direction * speed;

            var grounded = _controller.isGrounded;
            _sinceGrounded = grounded ? 0f : _sinceGrounded + Time.deltaTime;
            _sinceJumpPressed = _jump != null && _jump.WasPressedThisFrame() ? 0f : _sinceJumpPressed + Time.deltaTime;

            if (grounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            // Прыжок: с земли или чуть после схода с края; нажатие незадолго до приземления тоже засчитывается.
            if (_sinceJumpPressed <= jumpBuffer && _sinceGrounded <= coyoteTime && _verticalVelocity <= 0f)
            {
                _verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
                _sinceJumpPressed = float.MaxValue;
                _sinceGrounded = float.MaxValue;
            }

            _verticalVelocity += gravity * Time.deltaTime;

            var flags = _controller.Move((horizontal + Vector3.up * _verticalVelocity) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;

            Velocity = horizontal;
            Airborne = !_controller.isGrounded && _sinceGrounded > coyoteTime;
        }
    }
}
