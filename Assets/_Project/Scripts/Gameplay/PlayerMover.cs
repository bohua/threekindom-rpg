using UnityEngine;
using UnityEngine.InputSystem;

namespace Jrpg
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMover : MonoBehaviour
    {
        public float Speed = 3.5f;
        public bool InputEnabled = true;
        public InputActionReference MoveAction;
        CharacterController controller;
        void Awake() => controller = GetComponent<CharacterController>();
        void OnEnable() { if (MoveAction) MoveAction.action.Enable(); }
        void OnDisable() { if (MoveAction) MoveAction.action.Disable(); }
        void Update()
        {
            Vector2 input = InputEnabled && MoveAction ? MoveAction.action.ReadValue<Vector2>() : Vector2.zero;
            Move(input, Time.deltaTime);
        }
        public void Move(Vector2 input, float seconds)
        {
            Vector2 bounded = Vector2.ClampMagnitude(input, 1);
            controller.Move(new Vector3(bounded.x * Speed, -9.81f, bounded.y * Speed) * seconds);
        }
    }
}
