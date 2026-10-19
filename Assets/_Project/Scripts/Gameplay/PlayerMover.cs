using UnityEngine;

namespace Jrpg
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMover : MonoBehaviour
    {
        public float Speed = 3.5f;
        public bool InputEnabled = true;
        CharacterController controller;
        void Awake() => controller = GetComponent<CharacterController>();
        void Update()
        {
            Vector2 input = InputEnabled ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) : Vector2.zero;
            Move(input, Time.deltaTime);
        }
        public void Move(Vector2 input, float seconds)
        {
            Vector2 bounded = Vector2.ClampMagnitude(input, 1);
            controller.Move(new Vector3(bounded.x * Speed, -9.81f, bounded.y * Speed) * seconds);
        }
    }
}
