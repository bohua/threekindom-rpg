using UnityEngine;

namespace Jrpg
{
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0, 8, -10);
        void LateUpdate()
        {
            if (Target) transform.position = Target.position + Offset;
        }
    }
}
