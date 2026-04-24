using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class AIStateMachine : MonoBehaviour
    {
        [SerializeField] private AIState initialState = AIState.Idle;
        [SerializeField] private float hearingThreshold = 0.15f;
        [SerializeField] private float investigateDuration = 4f;
        [SerializeField] private CharacterMotor motor;

        private float _stateUntil;
        private Vector3 _investigationPoint;

        public AIState State { get; private set; }
        public Vector3 InvestigationPoint => _investigationPoint;

        private void Reset()
        {
            motor = GetComponent<CharacterMotor>();
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<CharacterMotor>();
            }

            SetState(initialState);
        }

        private void OnEnable()
        {
            NoiseEmitter.NoiseEmitted += OnNoiseEmitted;
        }

        private void OnDisable()
        {
            NoiseEmitter.NoiseEmitted -= OnNoiseEmitted;
        }

        private void Update()
        {
            if (State == AIState.Investigate && Time.time >= _stateUntil)
            {
                SetState(AIState.Alert);
            }
        }

        public void SetState(AIState nextState)
        {
            State = nextState;
            if (motor != null && State == AIState.Idle)
            {
                motor.SetMoveInput(Vector2.zero);
            }
        }

        public void ForceInvestigate(Vector3 point)
        {
            _investigationPoint = point;
            _stateUntil = Time.time + investigateDuration;
            SetState(AIState.Investigate);
        }

        private void OnNoiseEmitted(NoiseEvent noise)
        {
            if (noise.Source == gameObject || noise.Intensity < hearingThreshold)
            {
                return;
            }

            var distance = Vector3.Distance(transform.position, noise.Position);
            if (distance <= noise.Radius)
            {
                ForceInvestigate(noise.Position);
            }
        }
    }

    public enum AIState
    {
        Idle,
        Patrol,
        Investigate,
        Alert,
        Engage
    }
}
