using BreachScenarioEngine.Runtime;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace BreachScenarioEngine.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class PlayerInputBridge : MonoBehaviour
    {
        [SerializeField] private CharacterMotor motor;

        private Vector2 _moveInput;

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
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            ReadInputSystem();
#else
            ReadLegacyInput();
#endif
            motor.SetMoveInput(_moveInput);
        }

#if ENABLE_INPUT_SYSTEM
        private void ReadInputSystem()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                _moveInput = Vector2.zero;
                return;
            }

            _moveInput = new Vector2(
                ReadAxis(keyboard.aKey, keyboard.dKey, keyboard.leftArrowKey, keyboard.rightArrowKey),
                ReadAxis(keyboard.sKey, keyboard.wKey, keyboard.downArrowKey, keyboard.upArrowKey));
        }

        private static float ReadAxis(ButtonControl negative, ButtonControl positive, ButtonControl negativeAlt, ButtonControl positiveAlt)
        {
            var value = 0f;
            if (negative.isPressed || negativeAlt.isPressed) value -= 1f;
            if (positive.isPressed || positiveAlt.isPressed) value += 1f;
            return value;
        }
#else
        private void ReadLegacyInput()
        {
            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }
#endif
    }
}
