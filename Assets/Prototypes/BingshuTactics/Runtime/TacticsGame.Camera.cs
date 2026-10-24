// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-24
// 镜头（用户 2026-10-24）：镜头拉近，允许部分地图和人物在画面外；镜头跟随行动者与光标；
// 右摇杆／鼠标中键拖动／滚轮可自由查看与缩放；1–4 键或 LT／RT 直接对准人物，点行动条头像也能对准；右下角小地图显示全局，点小地图跳转。
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        const float CamDistance = 24f;        // 原来约 44 米，拉近到约一半
        Vector3 camTarget, camVel, manualTarget;
        bool manual, camInit; float zoomLevel = 1f;
        Unit lastActive; Vector2Int lastCursor;
        int focusIndex = -1;

        void FocusOn(Vector3 world) { manualTarget = world; manual = true; }
        void FocusOn(Unit u) { if (u != null) FocusOn(Tile(u.Pos)); }

        Vector3 AutoTarget()
        {
            bool targeting = mode == Mode.TargetMove || mode == Mode.TargetAttack || mode == Mode.TargetSkill || mode == Mode.Deploy;
            if (targeting && padMode && grid.In(cursor)) return Tile(cursor);
            if (active != null && mode != Mode.Deploy && mode != Mode.Circuit) return views[active].position;
            var party = units.Where(u => u.Team == Team.Player && u.Alive).ToList();
            if (party.Count == 0) return lookAt;
            var c = Vector3.zero; foreach (var u in party) c += views[u].position;
            return Vector3.Lerp(c / party.Count, lookAt, 0.35f);   // 布阵时偏向全场，看得到前方
        }

        void UpdateCamera()
        {
            // 行动者换人或光标移动时，取消手动查看，回到自动跟随
            if (active != lastActive || (padMode && cursor != lastCursor)) manual = false;
            lastActive = active; lastCursor = cursor;

            // 自由查看：右摇杆、鼠标中键拖动；滚轮缩放
            var right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            var fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector2 pan = Vector2.zero;
            if (Pad != null) pan += Pad.rightStick.ReadValue();
            var mouse = Mouse.current;
            if (mouse != null && mouse.middleButton.isPressed) pan -= mouse.delta.ReadValue() * 0.06f;
            if (pan.sqrMagnitude > 0.04f && mode != Mode.Circuit && mode != Mode.Duel)
            {
                if (!manual) { manualTarget = camTarget; manual = true; }
                manualTarget += (right * pan.x + fwd * pan.y) * Time.deltaTime * 14f;
                manualTarget.x = Mathf.Clamp(manualTarget.x, 0, W * T); manualTarget.z = Mathf.Clamp(manualTarget.z, 0, H * T);
            }
            if (mouse != null && mode != Mode.Circuit)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) zoomLevel = Mathf.Clamp(zoomLevel - Mathf.Sign(scroll) * 0.1f, 0.6f, 1.9f);
            }
            // 直接对准人物：1–4 键（刘备、关羽、张飞、吕布），LT／RT 依次切换
            if (Kb != null && mode != Mode.Circuit)
            {
                string[] names = { "刘备", "关羽", "张飞", "吕布" };
                var keys = new[] { Kb.digit1Key, Kb.digit2Key, Kb.digit3Key, Kb.digit4Key };
                for (int i = 0; i < 4; i++) if (keys[i].wasPressedThisFrame) FocusOn(units.FirstOrDefault(u => u.Name == names[i] && u.Alive));
            }
            if (Pad != null && mode != Mode.Circuit && (Pad.leftTrigger.wasPressedThisFrame || Pad.rightTrigger.wasPressedThisFrame))
            {
                var alive = units.Where(u => u.Alive).ToList();
                focusIndex = (focusIndex + (Pad.rightTrigger.wasPressedThisFrame ? 1 : alive.Count - 1)) % alive.Count;
                FocusOn(alive[focusIndex]);
            }

            var target = manual ? manualTarget : AutoTarget();
            if (!camInit) { camTarget = target; camInit = true; }
            camTarget = Vector3.SmoothDamp(camTarget, target, ref camVel, 0.25f);

            var rot = Quaternion.Euler(45f, yaw, 0f);
            camZoom = Mathf.MoveTowards(camZoom, camFocus.HasValue ? 1f : 0f, Time.deltaTime * 2.5f);
            if (camFocus.HasValue) duelLook = camFocus.Value;
            float z = Mathf.SmoothStep(0, 1, camZoom);
            var look = Vector3.Lerp(camTarget, duelLook, z);
            float dist = CamDistance * zoomLevel * (1f - 0.4f * z);
            cam.transform.SetPositionAndRotation(look - rot * Vector3.forward * dist, rot);
        }

        // ---------------- 小地图 ----------------
        Texture2D minimapTex; Rect minimapRect;

        void BuildMinimap()
        {
            minimapTex = new Texture2D(W, H) { filterMode = FilterMode.Point };
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var t = grid.TerrainAt(new Vector2Int(x, y)); int h = grid.HeightAt(new Vector2Int(x, y));
                    Color c = t switch
                    {
                        Terrain.River => new Color(0.22f, 0.40f, 0.62f), Terrain.Ford => new Color(0.42f, 0.58f, 0.68f),
                        Terrain.Sand => new Color(0.78f, 0.68f, 0.46f), Terrain.Road or Terrain.Bridge => new Color(0.55f, 0.47f, 0.36f),
                        Terrain.Camp => new Color(0.50f, 0.40f, 0.28f), Terrain.Wall => new Color(0.62f, 0.52f, 0.38f),
                        Terrain.Gate => new Color(0.30f, 0.18f, 0.12f), Terrain.JuMa => new Color(0.38f, 0.26f, 0.16f),
                        Terrain.Rock => new Color(0.45f, 0.44f, 0.42f), _ => new Color(0.36f, 0.50f, 0.26f) * (1f + 0.12f * h)
                    };
                    c.a = 1; minimapTex.SetPixel(x, y, c);
                }
            minimapTex.Apply();
        }

        void DrawMinimap(float sw)
        {
            if (mode == Mode.Circuit || mode == Mode.Duel) return;
            if (!minimapTex) BuildMinimap();
            const float cell = 9f;
            minimapRect = new Rect(sw - W * cell - 14, RefH - H * cell - 112, W * cell, H * cell);
            Rect(new Rect(minimapRect.x - 4, minimapRect.y - 20, minimapRect.width + 8, minimapRect.height + 24), new Color(0.03f, 0.04f, 0.06f, 0.85f));
            GUI.Label(new Rect(minimapRect.x, minimapRect.y - 19, 120, 18), "全局（点击跳转）", small);
            // 纹理 y 向上，GUI y 向下：上下翻转绘制，使北在上
            GUI.DrawTextureWithTexCoords(minimapRect, minimapTex, new Rect(0, 1, 1, -1));
            foreach (var u in units.Where(x => x.Alive))
            {
                var p = new Vector2(minimapRect.x + (u.Pos.x + 0.5f) * cell, minimapRect.y + (H - u.Pos.y - 0.5f) * cell);
                float r = u.Name == "吕布" ? 4.5f : 3.2f;
                Disc(p, r + 1f, Color.black);
                Disc(p, r, u.Team == Team.Player ? (u == active ? new Color(1f, 0.9f, 0.3f) : new Color(0.35f, 0.65f, 1f)) : new Color(1f, 0.35f, 0.3f));
            }
            // 镜头中心
            var cpos = new Vector2(minimapRect.x + camTarget.x / T * cell, minimapRect.y + (H - camTarget.z / T) * cell);
            var o = GUI.color; GUI.color = new Color(1, 1, 1, 0.8f);
            GUI.DrawTexture(new Rect(cpos.x - 14, cpos.y - 1, 28, 2), white); GUI.DrawTexture(new Rect(cpos.x - 1, cpos.y - 14, 2, 28), white);
            GUI.color = o;
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && minimapRect.Contains(e.mousePosition))
            {
                float gx = (e.mousePosition.x - minimapRect.x) / cell, gy = H - (e.mousePosition.y - minimapRect.y) / cell;
                FocusOn(new Vector3(gx * T, 0, gy * T));
                e.Use();
            }
        }
    }
}
