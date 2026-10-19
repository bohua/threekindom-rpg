using System.Collections;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Battle
{
    // Scene-side presentation of one battle unit: a lit pixel quad (ADR-0005 §4) with a blob shadow and a
    // selection ring. Only plays what the controller tells it; it never reads or changes rules state.
    public sealed class BattleUnitView : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public SpriteFrameAnimator Sprite;
        public GameObject SelectionRing;
        [Tooltip("Frame shown while idle, facing the other side")] public int IdleFrame = 2;
        public int AttackFrame = 6;
        public int DefendFrame = 7;
        [Tooltip("The atlas attack pose faces left; flip it for units on the left side")] public bool FlipAttack;
        [Header("Card portrait (UI)")]
        public Texture2D Portrait;
        [Tooltip("Chest-up crop in portrait pixels (x, y from bottom-left, width, height)")]
        public RectInt PortraitCrop;

        Vector3 home;
        Renderer quad;
        MaterialPropertyBlock block;
        float alpha = 1f, flash;

        public Vector3 Home => home;

        void Awake()
        {
            home = transform.localPosition;
            quad = Sprite ? Sprite.Target : null;
            block = new MaterialPropertyBlock();
        }

        public void ResetView()
        {
            StopAllCoroutines();
            transform.localPosition = home;
            alpha = 1f; flash = 0f;
            gameObject.SetActive(true);
            ShowFrame(IdleFrame, false);
            SetSelected(false);
            ApplyTint();
        }

        public void SetSelected(bool on) { if (SelectionRing) SelectionRing.SetActive(on); }

        public void SetDefending(bool on) => ShowFrame(on ? DefendFrame : IdleFrame, false);

        void ShowFrame(int frame, bool flip)
        {
            if (!Sprite) return;
            Sprite.Frame = frame;
            Sprite.Apply();
            var s = Sprite.transform.localScale;
            s.x = Mathf.Abs(s.x) * (flip ? -1f : 1f);
            Sprite.transform.localScale = s;
        }

        // Step toward the target, strike, step back.
        public IEnumerator Lunge(Vector3 targetLocal, float speed)
        {
            Vector3 dir = targetLocal - home; dir.z = 0f;
            Vector3 strikeAt = home + dir.normalized * Mathf.Min(1.4f, dir.magnitude * 0.4f);
            yield return Move(home, strikeAt, 0.12f / speed);
            ShowFrame(AttackFrame, FlipAttack);
            yield return new WaitForSeconds(0.16f / speed);
            yield return Move(strikeAt, home, 0.14f / speed);
            ShowFrame(IdleFrame, false);
        }

        public IEnumerator Cast(float speed)
        {
            ShowFrame(AttackFrame, FlipAttack);
            flash = 0.6f; ApplyTint();
            yield return new WaitForSeconds(0.25f / speed);
            flash = 0f; ApplyTint();
            ShowFrame(IdleFrame, false);
        }

        public IEnumerator Hit(float speed)
        {
            flash = 1f;
            for (float t = 0f; t < 0.22f / speed; t += Time.deltaTime)
            {
                float k = t * speed / 0.22f;
                transform.localPosition = home + new Vector3(Mathf.Sin(k * 40f) * 0.08f * (1f - k), 0f, 0f);
                flash = 1f - k; ApplyTint();
                yield return null;
            }
            transform.localPosition = home;
            flash = 0f; ApplyTint();
        }

        // Unit breaks and leaves the field: no death or execution is shown (V1_SPEC §2.2).
        public IEnumerator Retreat(float speed)
        {
            Vector3 away = home + new Vector3(Mathf.Sign(home.x) * 1.2f, 0f, 0f);
            for (float t = 0f; t < 0.5f / speed; t += Time.deltaTime)
            {
                float k = t * speed / 0.5f;
                transform.localPosition = Vector3.Lerp(home, away, k);
                alpha = 1f - k; ApplyTint();
                yield return null;
            }
            gameObject.SetActive(false);
        }

        IEnumerator Move(Vector3 from, Vector3 to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.localPosition = Vector3.Lerp(from, to, t / duration);
                yield return null;
            }
            transform.localPosition = to;
        }

        void ApplyTint()
        {
            if (!quad) return;
            quad.GetPropertyBlock(block);
            Color c = Color.Lerp(Color.white, new Color(2.2f, 1.6f, 1.4f), flash);
            c.a = alpha;
            block.SetColor(BaseColor, c);
            quad.SetPropertyBlock(block);
            // Binary alpha (alpha clip) cannot fade, so hide the shadow and dither out via scale when leaving.
            if (alpha < 1f) quad.transform.parent.localScale = new Vector3(1f, Mathf.Max(0.05f, alpha), 1f);
            else quad.transform.parent.localScale = Vector3.one;
        }
    }
}
