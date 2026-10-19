using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // Shows one frame of a horizontal/grid atlas on a URP Lit quad by setting _BaseMap_ST per renderer.
    [ExecuteAlways]
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        public Renderer Target;
        public int Columns = 9;
        public int Rows = 1;
        public int Frame;
        MaterialPropertyBlock block;
        int shown = -1;

        void OnEnable() => shown = -1;

        void LateUpdate()
        {
            if (Frame != shown) Apply();
        }

        public void Apply()
        {
            if (!Target) return;
            block ??= new MaterialPropertyBlock();
            int frame = Mathf.Clamp(Frame, 0, Columns * Rows - 1);
            float w = 1f / Columns, h = 1f / Rows;
            int column = frame % Columns, row = frame / Columns;
            Target.GetPropertyBlock(block);
            block.SetVector(BaseMapST, new Vector4(w, h, column * w, 1f - (row + 1) * h));
            Target.SetPropertyBlock(block);
            shown = Frame;
        }
    }
}
