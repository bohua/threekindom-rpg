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
                case FieldStoryController.Place.Suanzao:
                    yield return Person(p, "gu_sou 鼓叟", Q("NpcGuSou", Resident("N09"), 35f, 1.1f), new Vector3(7.4f, 0, -23.4f));
                    yield return Person(p, "zang_hong 臧洪", Q("NpcZangHong", Folk("D07"), 90f), new Vector3(8.6f, 0, 21.2f));
                    yield return Person(p, "bao_xin 鲍信", Q("NpcBaoXin", Folk("D08"), 150f), new Vector3(10.8f, 0, 14.6f));
                    yield return Person(p, "bao_tao 鲍韬", Q("NpcBaoTao", Folk("D08"), 175f, 0.8f, 1.1f), new Vector3(14f, 0, 12.2f));
                    break;
                case FieldStoryController.Place.Pingyuan:
                {
                    float cx = BohaiPlainBuilder.CityCx;
                    yield return Person(p, "gu_sou 鼓叟", Q("NpcGuSou", Resident("N09"), 35f, 1.1f), new Vector3(cx + 10f, 0, -186f));
                    yield return Person(p, "lu_ditou 鲁堤头", Q("NpcLuDitou", Resident("N11"), 60f), new Vector3(cx + 14f, BohaiPlainBuilder.LeveeH, -228f));
                    yield return Person(p, "fan_shen 范婶", Q("NpcFanShen", Resident("N08"), 90f), new Vector3(cx - 19f, 0, -158.6f));
                    yield return Person(p, "bian_sheng 卞生", Q("NpcBianSheng", Resident("N24"), 100f, 0.8f, 0.8f), new Vector3(cx - 27f, 0, -157f));
                    yield return Person(p, "a_he 阿禾", Q("NpcAHe16", Resident("N12"), -30f, 0.7f, 0.9f), new Vector3(-31.6f, 0, -149.4f));
                    yield return Person(p, "a_he_tow 阿禾", Q("NpcAHe16", Resident("N12"), -30f, 0.7f, 0.9f), new Vector3(cx + 9f, 0, -207f));
                    yield return Person(p, "zhang_shiping 张世平", Q("NpcZhangShiping", Resident("N20"), 120f), new Vector3(cx + 13f, 0, -183f));
                    yield return Person(p, "wu_laoli 伍老吏", Q("NpcWuLaoli", Resident("N03"), 60f), new Vector3(325f, 0, -174f));
                    yield return Person(p, "feng_widow 冯家娘子", Q("NpcFengWidow", Folk("E13"), 120f), new Vector3(337f, 0, -178f));
                    yield return Person(p, "feng_kuan 冯宽", Q("NpcFengKuan", Folk("E15"), 60f), new Vector3(339.6f, 0, -180f));
                    break;
                }
                case FieldStoryController.Place.Xiapi:
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

        // Stage actors the side quests use in the story stage (Stage3Actors' list goes on here).
        static IEnumerable<(string name, string key)> SideStageActors()
        {
            yield return ("NpcBaoXin", Q("NpcBaoXin", Folk("D08"), 150f));
        }
    }
}
