using UnityEngine;
using UnityEngine.InputSystem;

namespace Jrpg
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMover : MonoBehaviour
    {
        public float Speed = 9f;   // user 2026-10-20: twice the original 3.5 m/s, then "a bit faster"
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
        // Planar speed actually moved last frame (m/s), for the walk animation.
        public float CurrentSpeed { get; private set; }

        public void Move(Vector2 input, float seconds)
        {
            Vector2 bounded = Vector2.ClampMagnitude(input, 1);
            CurrentSpeed = bounded.magnitude * Speed;
            if (controller.enabled) controller.Move(new Vector3(bounded.x * Speed, -9.81f, bounded.y * Speed) * seconds);   // off while a story scene walks him
        }
    }
}
