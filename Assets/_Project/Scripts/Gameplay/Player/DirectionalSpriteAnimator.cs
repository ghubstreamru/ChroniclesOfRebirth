using UnityEngine;

namespace ChroniclesOfRebirth.Gameplay.Player
{
    /// <summary>
    /// Выбирает кадр спрайт-листа по направлению движения относительно камеры.
    /// Ряды листа сверху вниз: вниз, влево, вправо, вверх; кадр 0 — стоит, 1–3 — цикл шага.
    /// </summary>
    public sealed class DirectionalSpriteAnimator : MonoBehaviour
    {
        const int RowDown = 0, RowLeft = 1, RowRight = 2, RowUp = 3;
        static readonly int FrameRectId = Shader.PropertyToID("_FrameRect");
        static readonly int[] WalkCycle = { 1, 2, 3, 2 };
        const int AirFrame = 1;

        [SerializeField] Renderer targetRenderer;
        [SerializeField] PlayerController source;
        [SerializeField] int columns = 4;
        [SerializeField] int rows = 4;
        [SerializeField] float framesPerSecond = 8f;

        MaterialPropertyBlock _block;
        Transform _camera;
        int _row = RowDown;
        float _phase;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (Camera.main != null)
                _camera = Camera.main.transform;
        }

        void LateUpdate()
        {
            var velocity = source != null ? source.Velocity : Vector3.zero;
            var moving = velocity.sqrMagnitude > 0.01f;

            if (moving)
            {
                var forward = _camera != null ? Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized : Vector3.forward;
                var right = Vector3.Cross(Vector3.up, forward);
                var x = Vector3.Dot(velocity, right);
                var y = Vector3.Dot(velocity, forward);
                _row = Mathf.Abs(x) > Mathf.Abs(y) ? (x > 0f ? RowRight : RowLeft) : (y > 0f ? RowUp : RowDown);

                var speedFactor = source != null && source.WalkSpeed > 0f ? velocity.magnitude / source.WalkSpeed : 1f;
                _phase += Time.deltaTime * framesPerSecond * speedFactor;
            }
            else
            {
                _phase = 0f;
            }

            // В воздухе — застывший широкий шаг (отдельного кадра прыжка в листе пока нет).
            var airborne = source != null && source.Airborne;
            var frame = airborne ? AirFrame : moving ? WalkCycle[(int)_phase % WalkCycle.Length] : 0;
            var w = 1f / columns;
            var h = 1f / rows;

            // После перекомпиляции во время игры Awake не вызывается повторно — создаём блок по требованию.
            _block ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_block);
            _block.SetVector(FrameRectId, new Vector4(frame * w, 1f - (_row + 1) * h, w, h));
            targetRenderer.SetPropertyBlock(_block);
        }
    }
}
