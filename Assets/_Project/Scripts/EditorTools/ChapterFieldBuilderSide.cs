using System.Collections.Generic;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Chapter one's side-quest people in the walkable scenes (SideQuests*.cs decides who shows when). Positions keep the
    // doors free (user 2026-10-22) and stand people where the writers put them, as near as each built scene allows.
    // Until Codex's nine new people arrive (art/ch1-npc-*), everyone is an existing Q sheet with its clothes recoloured
    // (换色, the bible §8).
    public static partial class ChapterFieldBuilder
    {
        static string Q(string key, string from, float hue, float sat = 1f, float val = 1f) => NpcRecolor.Make(key, from, hue, sat, val);

        static IEnumerable<Transform> SidePeople(FieldStoryController.Place place, Transform p)
        {
            switch (place)
            {
                case FieldStoryController.Place.Camp:
                    yield return Person(p, "a_he 阿禾", Q("NpcAHe", Resident("N10"), -40f, 0.7f, 0.85f), new Vector3(-8.4f, 0, -4.4f));
                    yield return Person(p, "zheng_hao 郑蒿", Q("NpcZhengHao", Resident("N09"), -70f, 0.8f), new Vector3(-21f, 0, 6f));
                    break;
                case FieldStoryController.Place.Anxi:
                    yield return Person(p, "a_he 阿禾", Q("NpcAHe", Resident("N10"), -40f, 0.7f, 0.85f), new Vector3(-1.6f, 0, -20.2f));
                    yield return Person(p, "zhang_shiping 张世平", Q("NpcZhangShiping", Resident("N20"), 120f), new Vector3(10.6f, 0, -19.4f));
                    yield return Person(p, "su_shuang 苏双", Q("NpcSuShuang", Resident("N20"), 200f), new Vector3(12.8f, 0, -19.8f));
                    break;
                case FieldStoryController.Place.Gongsun:
                    yield return Person(p, FieldScriptsRegions.Courier + " 驿卒", Resident("N07"), new Vector3(4.4f, 0, -19.4f));
                    break;
                case FieldStoryController.Place.Suanzao:
                    yield return Person(p, FieldScriptsRegions.Courier + " 驿卒", Resident("N07"), new Vector3(-3.2f, 0, -24.2f));
                    yield return Person(p, "gu_sou 鼓叟", Q("NpcGuSou", Resident("N09"), 35f, 1.1f), new Vector3(7.4f, 0, -23.4f));
                    yield return Person(p, "zang_hong 臧洪", Q("NpcZangHong", Folk("D07"), 90f), new Vector3(8.6f, 0, 21.2f));
                    yield return Person(p, "bao_xin 鲍信", Q("NpcBaoXin", Folk("D08"), 150f), new Vector3(10.8f, 0, 14.6f));
                    yield return Person(p, "bao_tao 鲍韬", Q("NpcBaoTao", Folk("D08"), 175f, 0.8f, 1.1f), new Vector3(14f, 0, 12.2f));
                    break;
                case FieldStoryController.Place.Pingyuan:
                {
                    float cx = BohaiPlainBuilder.CityCx;
                    yield return Person(p, FieldScriptsRegions.Courier + " 驿卒", Resident("N07"), new Vector3(cx - 4f, 0, -187.4f));
                    yield return Person(p, "gu_sou 鼓叟", Q("NpcGuSou", Resident("N09"), 35f, 1.1f), new Vector3(cx + 10f, 0, -186f));
                    yield return Person(p, "lu_ditou 鲁堤头", Q("NpcLuDitou", Resident("N11"), 60f), new Vector3(cx + 14f, BohaiPlainBuilder.LeveeH, -228f));
                    yield return Person(p, "fan_shen 范婶", Q("NpcFanShen", Resident("N08"), 90f), new Vector3(cx - 19f, 0, -158.6f));
                    yield return Person(p, "bian_sheng 卞生", Q("NpcBianSheng", Resident("N24"), 100f, 0.8f, 0.8f), new Vector3(cx - 27f, 0, -157f));
                    yield return Person(p, "a_he 阿禾", Q("NpcAHe16", Resident("N12"), -30f, 0.7f, 0.9f), new Vector3(-31.6f, 0, -149.4f));
                    yield return Person(p, "a_he_tow 阿禾", Q("NpcAHe16", Resident("N12"), -30f, 0.7f, 0.9f), new Vector3(cx + 9f, 0, -207f));
                    yield return Person(p, "zhang_shiping 张世平", Q("NpcZhangShiping", Resident("N20"), 120f), new Vector3(cx + 13f, 0, -183f));
                    yield return Person(p, "qz_aide 青州从事", Q("NpcQzAide", Resident("N03"), 140f), new Vector3(cx + 6f, 0, -146.5f));
                    yield return Person(p, "wu_laoli 伍老吏", Q("NpcWuLaoli", Resident("N03"), 60f), new Vector3(325f, 0, -174f));
                    yield return Person(p, "feng_widow 冯家娘子", Q("NpcFengWidow", Folk("E13"), 120f), new Vector3(337f, 0, -178f));
                    yield return Person(p, "feng_kuan 冯宽", Q("NpcFengKuan", Folk("E15"), 60f), new Vector3(339.6f, 0, -180f));
                    break;
                }
                case FieldStoryController.Place.R3:
                    // The old camp's ruin: the grave outside the west palisade, the herb woman beside it; Ye's south gate.
                    yield return Person(p, "zheng_grave 郑蒿之墓", Q("NpcGraveBoard", Resident("N09"), 0f, 0f, 0.55f), Ground(new Vector3(344f, 0, 224f)));
                    yield return Person(p, "ge_niang 葛娘", Q("NpcGeNiang", Resident("N08"), -40f, 0.8f, 0.9f), Ground(new Vector3(346f, 0, 214f)));
                    yield return Person(p, "ye_captain 邺城队率", Q("NpcYeCaptain", Folk("E06"), 0f), Ground(new Vector3(154f, 0, 112f)));
                    break;
                case FieldStoryController.Place.R6:
                    // Puyang's market (Gu Sou, the patrol, hidden until he comes); the new grave between Hua and Fei.
                    yield return Person(p, "gu_sou 鼓叟", Q("NpcGuSou", Resident("N09"), 35f, 1.1f), Ground(new Vector3(226f, 0, 222f)));
                    yield return Person(p, "puyang_patrol 曹军巡兵", Folk("F11"), Ground(new Vector3(233f, 0, 226f)));
                    yield return Person(p, "huafei_headman 华县里正", Q("NpcHuafeiHeadman", Resident("N03"), 30f, 0.8f), Ground(new Vector3(430f, 0, 104f)));
                    yield return Person(p, "huafei_peddler 费县货郎", Q("NpcHuafeiPeddler", Resident("N20"), -50f), Ground(new Vector3(418f, 0, 116f)));
                    yield return Person(p, "taishan_woodcutter 泰山樵夫", Q("NpcTaishanWoodcutter", Resident("N11"), 100f, 0.8f), Ground(new Vector3(446f, 0, 120f)));
                    break;
                case FieldStoryController.Place.R5a:
                    yield return Person(p, "lao_hang 汴水船夫", Q("NpcLaoHang", Resident("N11"), 190f, 0.7f, 0.9f), Ground(new Vector3(296f, 0, 131f)));
                    break;
                case FieldStoryController.Place.R2:
                    // The Jingxing crossroads post station (Gu Sou, the horse pen) and the old angler below the Hutuo ferry.
                    yield return Person(p, "gu_sou 鼓叟", Q("NpcGuSou", Resident("N09"), 35f, 1.1f), Ground(new Vector3(146f, 0, 130f)));
                    yield return Person(p, "zhang_shiping 张世平", Q("NpcZhangShiping", Resident("N20"), 120f), Ground(new Vector3(132f, 0, 138f)));
                    yield return Person(p, "su_shuang 苏双", Q("NpcSuShuang", Resident("N20"), 200f), Ground(new Vector3(130f, 0, 139.4f)));
                    yield return Person(p, "niu_lao 牛老", Q("NpcNiuLao", Resident("N09"), 160f, 0.7f), Ground(new Vector3(283f, 0, 82f)));
                    break;
                case FieldStoryController.Place.R7:
                    // The Jixia ruins outside Linzi's west gate; Jinan's prefecture steps and the new shrine in the oak wood;
                    // Xiami's post station by the east road; Gu Sou at Duchang; the horse merchants by Jinan's post road.
                    yield return Person(p, "chunyu_weng 淳于翁", Q("NpcChunyuWeng", Resident("N24"), 200f, 0.6f, 0.95f), Ground(new Vector3(294f, 0, 228f)));
                    yield return Person(p, "jixia_stele 残碑", Q("NpcStele", Resident("N03"), 0f, 0f, 0.6f), Ground(new Vector3(301f, 0, 236f)));
                    yield return Person(p, "yan_bo 鄢伯", Q("NpcYanBo", Resident("N03"), 90f, 0.7f, 0.9f), Ground(new Vector3(84f, 0, 208f)));
                    yield return Person(p, "jingwang_shrine 景王祠", Q("NpcShrinePriest", Resident("N09"), 130f, 1f, 0.9f), Ground(new Vector3(104f, 0, 238f)));
                    yield return Person(p, "bu_heng 卜衡", Q("NpcBuHeng", Resident("N07"), 60f, 0.8f), Ground(new Vector3(395f, 0, 200f)));
                    yield return Person(p, "gu_sou 鼓叟", Q("NpcGuSou", Resident("N09"), 35f, 1.1f), Ground(new Vector3(470f, 0, 199f)));
                    yield return Person(p, "zhang_shiping 张世平", Q("NpcZhangShiping", Resident("N20"), 120f), Ground(new Vector3(98f, 0, 196f)));
                    yield return Person(p, "su_shuang 苏双", Q("NpcSuShuang", Resident("N20"), 200f), Ground(new Vector3(100.4f, 0, 195.4f)));
                    break;
                case FieldStoryController.Place.R5b:
                    // Luoyang: the fire by the South Palace's platform, the old scholar at its square's edge, the well,
                    // the tomb warden at Beimang's gate tower, the hungry family on the west slope (hidden until asked).
                    yield return Person(p, "gu_sou 鼓叟", Q("NpcGuSou", Resident("N09"), 35f, 1.1f), Ground(new Vector3(253f, 0, 176f)));
                    yield return Person(p, "guan_hu 管虎", Q("NpcGuanHu", Resident("N09"), -90f, 0.7f, 0.85f), Ground(new Vector3(256f, 0, 174.6f)));
                    yield return Person(p, "jiang_sanniang 姜三娘", Q("NpcJiangSanniang", Resident("N08"), 160f, 0.7f, 0.85f), Ground(new Vector3(250.4f, 0, 174.4f)));
                    yield return Person(p, "qi_gong 祁恭", Q("NpcQiGong", Resident("N24"), 180f, 0.6f, 0.9f), Ground(new Vector3(241f, 0, 172f)));
                    yield return Person(p, "fan_wu 樊伍", Q("NpcFanWu", Folk("F16"), 40f, 0.8f), Ground(new Vector3(300.6f, 0, 140.6f)));
                    yield return Person(p, "yan_gu 严固", Q("NpcYanGu", Resident("N03"), -120f, 0.6f, 0.85f), Ground(new Vector3(256f, 0, 262f)));
                    yield return Person(p, "mang_digger 挖冢的汉子", Q("NpcMangDigger", Resident("N11"), -60f, 0.6f, 0.8f), Ground(new Vector3(112f, 0, 252f)));
                    yield return Person(p, "yuan_agent 袁家来人", Q("NpcYuanAgent", Folk("D13"), 60f), Ground(new Vector3(304f, 0, 137f)));
                    break;
                case FieldStoryController.Place.Xiapi:
                    yield return Person(p, FieldScriptsRegions.Courier + " 驿卒", Resident("N07"), new Vector3(-2.4f, 0, -36.4f));
                    yield return Person(p, "gu_sou 鼓叟", Q("NpcGuSou", Resident("N09"), 35f, 1.1f), new Vector3(-12f, 0, 10f));
                    yield return Person(p, "a_he 阿禾", Q("NpcAHe16", Resident("N12"), -30f, 0.7f, 0.9f), new Vector3(-10f, 0, -30f));
                    yield return Person(p, "zhang_shiping 张世平", Q("NpcZhangShiping", Resident("N20"), 120f), new Vector3(-20f, 0, -21f));
                    yield return Person(p, "su_shuang 苏双", Q("NpcSuShuang", Resident("N20"), 200f), new Vector3(-17.8f, 0, -21.4f));
                    yield return Person(p, "chu_weng 褚翁", Q("NpcChuWeng", Resident("N09"), 150f, 0.8f, 0.9f), new Vector3(-14f, 0, -36f));
                    yield return Person(p, "yao_heng 姚衡", Q("NpcYaoHeng", Resident("N03"), 200f), new Vector3(-11.6f, 0, -37.4f));
                    yield return Person(p, "pan_daqiao 潘大樵", Folk("F13"), new Vector3(14f, 0, -8f));
                    // The other nine of his squad, scattered through the city (the card's list, mapped onto this square).
                    var spots = new[]
                    {
                        new Vector3(16.4f, 0, -9.4f), new Vector3(-21f, 0, -24f), new Vector3(-9f, 0, -28.4f), new Vector3(18f, 0, -33f),
                        new Vector3(10f, 0, -24f), new Vector3(-2.6f, 0, 16f), new Vector3(-26f, 0, -19.6f), new Vector3(-16f, 0, -33.6f),
                        new Vector3(2.4f, 0, -38.6f),
                    };
                    for (int i = 0; i < SideQuestsR8.Shi.Length; i++)
                        yield return Person(p, SideQuestsR8.Shi[i] + " 丹杨兵", Q("NpcDanyang" + (i % 3), Folk("F13"), i % 3 * 20f, 1f, 0.92f + i % 3 * 0.05f), spots[i]);
                    break;
            }
        }

        // Things to look at in a region's story layer (the side quests' spots), none yet.
        static IEnumerable<Transform> SideSpots(FieldStoryController.Place place, Transform spots)
        {
            yield break;
        }

        // Stage actors the side quests use in the story stage (Stage3Actors' list goes on here).
        static IEnumerable<(string name, string key)> SideStageActors()
        {
            yield return ("NpcBaoXin", Q("NpcBaoXin", Folk("D08"), 150f));
        }
    }
}
