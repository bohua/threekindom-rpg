using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Imports the Codex Zhuojun residents (ZHUOJUN_RESIDENTS.md): twelve 3×3 pose sheets, three of them with a 4×4 walk
    // sheet, all on 418-px cells with one shared root (209, 378). Each becomes a pixel atlas like the heroes': frames 0–8
    // the poses, 9–24 the walk cycle when there is one. Sizes are measured against Liu Bei's walk sheet, so a child stays
    // a child and adults match the heroes (no per-figure normalising).
    public static class ResidentImporter
    {
        const string DefaultSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\zhuojun-residents";
        const string LiuBeiWalk = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\walk-cycles\LIU_BEI_WALK_4X4.png";
        const int LiuBeiFigure = 44;   // Liu Bei's FigureHeight in the hero atlases

        // NPC id, atlas key, file prefix, has a walk sheet.
        public static readonly (string Id, string Key, string Prefix, bool Walks)[] Residents =
        {
            ("N01", "NpcGateGuard", "NPC_GATE_GUARD", false),
            ("N02", "NpcPatrolSoldier", "NPC_PATROL_SOLDIER", true),
            ("N03", "NpcCastleSteward", "NPC_CASTLE_STEWARD", false),
            ("N04", "NpcBlacksmith", "NPC_BLACKSMITH", false),
            ("N05", "NpcTavernKeeper", "NPC_TAVERN_KEEPER", false),
            ("N06", "NpcPhysician", "NPC_PHYSICIAN", false),
            ("N07", "NpcCourier", "NPC_COURIER", false),
            ("N08", "NpcMatWeaver", "NPC_MAT_WEAVER", false),
            ("N09", "NpcElder", "NPC_ELDER", false),
            ("N10", "NpcChild", "NPC_CHILD", true),
            ("N11", "NpcGrainSeller", "NPC_GRAIN_SELLER", false),
            ("N12", "NpcGrainPorter", "NPC_GRAIN_PORTER", true),
        };

        // The second batch of townsfolk (user 2026-10-22, Codex art/zhuojun-npcs-2), same sheet format. Until a sheet
        // is delivered the town uses the stand-in resident's atlas (StandIn).
        public static readonly (string Id, string Key, string Prefix, bool Walks, string StandIn)[] Townsfolk =
        {
            ("N13", "NpcDrillSoldier", "NPC_DRILL_SOLDIER", true, "N02"),
            ("N14", "NpcArcherSoldier", "NPC_ARCHER_SOLDIER", false, "N02"),
            ("N15", "NpcDrillOfficer", "NPC_DRILL_OFFICER", false, "N01"),
            ("N16", "NpcNoticeMan", "NPC_NOTICE_MAN", false, "N11"),
            ("N17", "NpcNoticeWoman", "NPC_NOTICE_WOMAN", false, "N08"),
            ("N18", "NpcJuggler", "NPC_JUGGLER", false, "N07"),
            ("N19", "NpcSpearPerformer", "NPC_SPEAR_PERFORMER", false, "N12"),
            ("N20", "NpcStallMerchant", "NPC_STALL_MERCHANT", false, "N11"),
            ("N21", "NpcWasherwoman", "NPC_WASHERWOMAN", false, "N08"),
            ("N22", "NpcChildBoy", "NPC_CHILD_BOY", true, "N10"),
            ("N23", "NpcChildGirl", "NPC_CHILD_GIRL", true, "N10"),
            ("N24", "NpcScholar", "NPC_SCHOLAR", false, "N06"),
            ("N25", "NpcGossipMan", "NPC_GOSSIP_MAN", false, "N07"),
            ("N26", "NpcGossipWoman", "NPC_GOSSIP_WOMAN", false, "N08"),
            ("N27", "NpcFarmer", "NPC_FARMER", true, "N12"),
            ("N28", "NpcRecruitClerk", "NPC_RECRUIT_CLERK", false, "N03"),
            ("N29", "NpcRecruitYouth", "NPC_RECRUIT_YOUTH", true, "N12"),
            ("N30", "NpcBeggar", "NPC_BEGGAR", false, "N09"),
            ("N31", "NpcRuffian", "NPC_RUFFIAN", false, "N12"),
            ("N32", "NpcCasinoDealer", "NPC_CASINO_DEALER", false, "N05"),
        };
        const string TownsfolkSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\zhuojun-npcs-2";

        // 0.2 黄巾篇 people (Codex art/v02-characters-sprites), same sheet format; until delivered, the stand-in's atlas.
        // The bandits, the guards and Dong Zhuo also stand in battle.
        public static readonly (string Id, string Key, string Prefix, bool Walks, string StandIn)[] Chapter2 =
        {
            ("C01", "NpcLuZhi", "LU_ZHI", false, "N03"),
            ("C02", "NpcCaoCao", "CAO_CAO", false, "N02"),
            ("C03", "NpcDongZhuo", "DONG_ZHUO", false, "N01"),
            ("C04", "NpcZouJing", "ZOU_JING", false, "N01"),
            ("C06", "NpcDuYou", "DU_YOU", false, "N03"),
            ("C07", "NpcBanditChief", "BANDIT_CHIEF", false, "N12"),
            ("C08", "NpcBandit", "BANDIT", false, "N12"),
            ("C09", "NpcHanSoldier", "HAN_SOLDIER", true, "N02"),
            ("C10", "NpcXiliangGuard", "XILIANG_GUARD", false, "N01"),
            ("C11", "NpcYellowGuard", "YELLOW_GUARD", false, "N02"),
            ("C12", "NpcEscortOfficer", "ESCORT_OFFICER", false, "N01"),
            ("C13", "NpcHanOfficer", "HAN_OFFICER", false, "N01"),
        };
        const string Chapter2Source = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\v02-characters-sprites";

        // A 0.2 person's atlas now: their own once imported, else the stand-in resident's.
        public static string ChapterKey(string id)
        {
            var c = Chapter2.First(x => x.Id == id);
            return File.Exists(HD2DSampleBuilder.AtlasPath(c.Key)) ? c.Key : Residents.First(x => x.Id == c.StandIn).Key;
        }

        // The atlas a townsperson uses now: their own once imported, else their stand-in's.
        public static string KeyFor(string id)
        {
            var r = Residents.FirstOrDefault(x => x.Id == id);
            if (r.Key != null) return r.Key;
            var t = Townsfolk.First(x => x.Id == id);
            return File.Exists(HD2DSampleBuilder.AtlasPath(t.Key)) ? t.Key : Residents.First(x => x.Id == t.StandIn).Key;
        }

        // The tavern waiter (user 2026-10-22, Codex art/tavern-waiter): same sheet format, his own folder.
        public const string WaiterKey = "NpcTavernWaiter";
        const string WaiterSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\tavern-waiter";

        [MenuItem("JRPG/Import Zhuojun Residents")]
        public static void Import()
        {
            string root = System.Environment.GetEnvironmentVariable("HD2D_RESIDENTS_SOURCE");
            if (string.IsNullOrEmpty(root)) root = DefaultSource;
            if (!File.Exists(LiuBeiWalk)) { Debug.Log("Liu Bei walk sheet not found; keeping the committed resident atlases."); return; }
            // Reference heights: Liu Bei's down-row left contact (walk frame 0) and passing pose (walk frame 1, standing).
            float liuWalk0, liuStanding;
            using (var liu = new Sheet(LiuBeiWalk, 4)) { liuWalk0 = liu.FigureHeight(0); liuStanding = liu.FigureHeight(1); }
            int size = HD2DSampleBuilder.FrameSize;

            var sheets = Residents.Select(r => (r.Key, r.Prefix, r.Walks, Dir: root))
                .Append((WaiterKey, "NPC_TAVERN_WAITER", true, WaiterSource))
                .Concat(Townsfolk.Select(t => (t.Key, t.Prefix, t.Walks, Dir: TownsfolkSource)))
                .Concat(Chapter2.Select(c => (c.Key, c.Prefix, c.Walks, Dir: Chapter2Source)));
            foreach (var (key, prefix, walks, dir) in sheets)
            {
                string poses = Path.Combine(dir, prefix + "_3X3.png");
                if (!File.Exists(poses)) { Debug.Log($"Resident sheet {prefix} not found; keeping the committed atlas."); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(HD2DSampleBuilder.AtlasPath(key)));
                Color32[] posePixels, walkPixels = null;
                int poseWidth, walkWidth = 0;
                using (var sheet = new Sheet(poses, 3))
                    posePixels = sheet.Convert(Mathf.RoundToInt(LiuBeiFigure * sheet.FigureHeight(0) / liuStanding), size, out poseWidth);
                string walkPath = Path.Combine(dir, prefix + "_WALK_4X4.png");
                if (walks && File.Exists(walkPath))
                    using (var sheet = new Sheet(walkPath, 4))
                        walkPixels = sheet.Convert(Mathf.RoundToInt(LiuBeiFigure * sheet.FigureHeight(0) / liuWalk0), size, out walkWidth);

                int width = poseWidth + walkWidth;
                var atlas = new Texture2D(width, size, TextureFormat.RGBA32, false);
                var part = new Texture2D(poseWidth, size, TextureFormat.RGBA32, false);
                part.SetPixels32(posePixels);
                atlas.SetPixels(0, 0, poseWidth, size, part.GetPixels());
                if (walkPixels != null)
                {
                    var w = new Texture2D(walkWidth, size, TextureFormat.RGBA32, false);
                    w.SetPixels32(walkPixels);
                    atlas.SetPixels(poseWidth, 0, walkWidth, size, w.GetPixels());
                    Object.DestroyImmediate(w);
                }
                atlas.Apply();
                string path = HD2DSampleBuilder.AtlasPath(key);
                File.WriteAllBytes(path, atlas.EncodeToPNG());
                Object.DestroyImmediate(atlas); Object.DestroyImmediate(part);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                PixelSpriteConverter.ConfigureImporter(path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("RESIDENTS_IMPORTED");
        }

        // One square sheet of square cells sharing the root (209, 378) of a 418-px cell.
        sealed class Sheet : System.IDisposable
        {
            readonly Texture2D tex;
            readonly Color32[] px;
            readonly int grid;

            public Sheet(string path, int grid)
            {
                this.grid = grid;
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(path));
                px = tex.GetPixels32();
            }

            // Opaque height of a cell (frames counted row-major from the top-left), in source pixels.
            public float FigureHeight(int frame)
            {
                int cell = tex.width / grid, cx = frame % grid, cy = grid - 1 - frame / grid;
                int top = -1, bottom = int.MaxValue;
                for (int y = cy * cell; y < (cy + 1) * cell; y++)
                for (int x = cx * cell; x < (cx + 1) * cell; x++)
                    if (px[y * tex.width + x].a >= 128) { top = Mathf.Max(top, y); bottom = Mathf.Min(bottom, y); }
                return top < 0 ? 0f : top - bottom + 1;
            }

            public Color32[] Convert(int figureHeight, int frameSize, out int width)
            {
                var s = PixelSpriteConverter.Settings.Default;
                s.Columns = grid; s.Rows = grid; s.FrameSize = frameSize; s.FigureHeight = Mathf.Max(8, figureHeight);
                int cell = tex.width / grid;
                s.FixedRoot = new Vector2Int(209 * cell / 418, 378 * cell / 418);
                return PixelSpriteConverter.BuildAtlas(px, tex.width, tex.height, s, out width, out _);
            }

            public void Dispose() => Object.DestroyImmediate(tex);
        }
    }
}
