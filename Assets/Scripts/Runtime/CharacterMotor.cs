using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private Rigidbody2D body;

        private Vector2 _moveInput;
        private Vector2 _lookDirection = Vector2.up;

        public Vector2 Velocity { get; private set; }
        public Vector2 LookDirection => _lookDirection;
        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }
        }

        public void SetMoveInput(Vector2 input)
        {
            _moveInput = Vector2.ClampMagnitude(input, 1f);
            if (_moveInput.sqrMagnitude > 0.0001f)
            {
                _lookDirection = _moveInput.normalized;
            }
        }

        public void SetLookDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                _lookDirection = direction.normalized;
            }
        }

        private void FixedUpdate()
        {
            Velocity = _moveInput * moveSpeed;
            if (body != null)
            {
                body.linearVelocity = Velocity;
                RotateBody(Time.fixedDeltaTime);
                return;
            }

            transform.position += (Vector3)(Velocity * Time.fixedDeltaTime);
            RotateTransform(Time.fixedDeltaTime);
        }

        private void RotateBody(float deltaTime)
        {
            if (_lookDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetAngle = Mathf.Atan2(_lookDirection.y, _lookDirection.x) * Mathf.Rad2Deg - 90f;
            var angle = Mathf.MoveTowardsAngle(body.rotation, targetAngle, rotationSpeed * deltaTime);
            body.MoveRotation(angle);
        }

        private void RotateTransform(float deltaTime)
        {
            if (_lookDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetAngle = Mathf.Atan2(_lookDirection.y, _lookDirection.x) * Mathf.Rad2Deg - 90f;
            var current = transform.eulerAngles.z;
            var angle = Mathf.MoveTowardsAngle(current, targetAngle, rotationSpeed * deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
