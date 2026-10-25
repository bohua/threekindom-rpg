// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-24
// 虎牢关改用涿郡的 HD-2D 画面（用户 2026-10-24：“战场美术比较粗糙，看能否做成涿郡那种HD2D的贴图来展现虎牢关，地貌，营寨等细节”）：
// 同一套后期（景深、泛光、ACES、暗角、暖色 LUT，取自 Assets/_Project/Settings/HD2D_Default.asset）、
// 同一套三色环境光与暖雾、暖色斜阳，以及涿郡的原生贴图（每米 30 像素，地面 120×120 一张铺 4 米）。
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        public VolumeProfile HD2DProfile;   // 由编辑器构建脚本填入
        DepthOfField focusDof;

        const float ZTile = 4f;             // 涿郡地面贴图一张对应 4 米
        const float ZPropScale = 0.55f;

        // 涿郡贴图（缺图时退回虎牢关贴图包）
        string ZTop(Terrain t) => HdTop(t) ? "hd:" + HdTop(t).name : t switch
        {
            Terrain.Grass => "ground_grass", Terrain.Road => "ground_earth_ruts", Terrain.Sand => "ground_packed_earth", Terrain.Camp => "ground_earth_gravel", Terrain.Ford => "ground_ford_shallows", Terrain.Bridge => "bridge_deck",
            Terrain.Wall or Terrain.Gate => "citywall_walkway", _ => null
        };

        Material ZMat(string name)
        {
            if (name != null && name.StartsWith("hd:")) { var hd = HdGround.FirstOrDefault(x => x && x.name == name.Substring(3)); return hd ? LitMat("z_" + name, hd) : null; }
            var t = name == null ? null : Env(name);
            return t ? LitMat("z_" + name, t) : null;
        }

        void SetupHD2D(Light sun)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.80f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.70f, 0.64f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.30f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.78f, 0.74f, 0.68f);
            sun.color = new Color(1f, 0.92f, 0.80f); sun.intensity = 2.2f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.8f;
            sun.transform.rotation = Quaternion.Euler(42f, 150f, 0f);   // 斜阳从关墙一侧照来，关墙投影落在门前
            cam.backgroundColor = new Color(0.14f, 0.12f, 0.11f);

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            if (!HD2DProfile) return;
            var vol = new GameObject("HD2D Volume").AddComponent<Volume>();
            vol.isGlobal = true; vol.sharedProfile = HD2DProfile;
            // 景深单独放一层，焦点每帧对准镜头注视点（与涿郡 FollowCamera 相同做法，不改共享配置）
            if (HD2DProfile.TryGet<DepthOfField>(out var src) && src.active)
            {
                var ov = new GameObject("HD2D Focus").AddComponent<Volume>();
                ov.isGlobal = true; ov.priority = vol.priority + 1;
                ov.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                focusDof = Instantiate(src);
                ov.sharedProfile.components.Add(focusDof);
            }
        }

        void UpdateFocus(float distance)
        {
            if (focusDof == null) return;
            focusDof.focusDistance.overrideState = true;
            focusDof.focusDistance.value = distance;
            bool ui = mode == Mode.Circuit;   // 兵书回路是全屏界面，背景不需要景深
            focusDof.active = !ui;
        }
    }
}
