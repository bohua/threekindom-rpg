using System.Collections;
using ThreeKingdoms.Core.Story;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Story
{
    // Chapter 1 side quests in Zhuojun (design/chapter1-rise/npc-sidequests R1_R2, core layer; user 2026-10-29), the
    // "0.2 backfill" that only adds people and lines:
    //   · sq.lb1.L5.zhuo_horse 中山马商（一）: after battle 1 Zhang Shiping and Su Shuang wait outside the post station by
    //     the courier N07; they pay for weapons (memory zhuo_gift=accepted for both), and the smith N04 then tells how he
    //     forged the three weapons (剧本 §10.1 #1 — their "来历"; the keepsake system that will hold them is not built).
    //   · sq.lb1.L1.gu_sou_1 黄天当立: after battle 3 the drum-storyteller Gu Sou tells of Zhang Jue beside the innkeeper
    //     N05; after the tale Liu Bei owns up to being the mat-weaver, or only leaves coins (memory gu_sou.met_liu_bei).
    // The three stand in for the new Q sprites (Codex art/ch1-npc-sprites) as copies of townsfolk until those arrive.
    public sealed partial class TownStoryController
    {
        public const string ZhuoGift = "lb1.L5.zhuo_gift", ZhuoWeapons = "lb1.L5.zhuo_weapons", GuSouHeard = "lb1.L1.gu_sou_1";

        Transform zhangShiping, suShuang, guSou;
        bool hintedSmith;

        bool InZhuojunYears => Run.Chapter2.Step <= ChapterStep.ZhuojunFree;

        void SetUpSidequests()
        {
            if (!InZhuojunYears) return;
            var story = Run.Story;
            var courier = Resident("N07");
            if (story.BattlesDone >= 1 && courier)
            {
                zhangShiping = StandIn(Resident("N11"), "zhang_shiping 张世平", courier.position + new Vector3(2.6f, 0, 1.4f));
                suShuang = StandIn(Resident("N20") ?? Resident("N11"), "su_shuang 苏双", courier.position + new Vector3(4.2f, 0, 0.6f));
            }
            if (story.BattlesDone >= StoryState.BattleCount && Innkeeper)
                guSou = StandIn(Resident("N09"), "gu_sou 鼓叟", Innkeeper.position + new Vector3(2.4f, 0, -0.4f));
        }

        // A copy of a townsperson standing still at the spot, renamed for the prompt.
        static Transform StandIn(Transform like, string name, Vector3 at)
        {
            if (!like) return null;
            var t = Instantiate(like.gameObject, at, like.rotation, like.parent).transform;
            t.name = name;
            if (t.TryGetComponent<NpcPatrol>(out var patrol)) Destroy(patrol);
            t.gameObject.SetActive(true);
            return t;
        }

        string SidePrompt(Vector3 p)
        {
            if (Near(zhangShiping, p) || Near(suShuang, p)) return "side:merchants";
            if (Near(guSou, p)) return "side:gu_sou";
            if (Near(Smith, p) && Run.Chapter2.Has(ZhuoGift) && !Run.Chapter2.Has(ZhuoWeapons)) return "side:smith";
            return null;
        }

        bool StartSide(string prompt)
        {
            switch (prompt)
            {
                case "side:merchants": StartCoroutine(Hold(Merchants())); return true;
                case "side:gu_sou": StartCoroutine(Hold(GuSou())); return true;
                case "side:smith": StartCoroutine(Hold(SmithGift())); return true;
            }
            return false;
        }

        // Once, before the gate takes them out of town: the smith is still waiting with the weapons (the design's reminder).
        bool HintSmith()
        {
            if (hintedSmith || !Run.Chapter2.Has(ZhuoGift) || Run.Chapter2.Has(ZhuoWeapons)) return false;
            hintedSmith = true;
            StartCoroutine(Hold(Say("system", "dlg.lb1.zhuo_horse.hint")));
            return true;
        }

        IEnumerator Merchants()
        {
            var ch = Run.Chapter2;
            Face(suShuang);
            if (ch.Has(ZhuoGift)) { yield return Say("su_shuang", "dlg.lb1.su_shuang.N1.idle"); yield break; }
            yield return Lines(new[]
            {
                new DialogueLine("su_shuang", "dlg.lb1.su_shuang.N1.1"), new DialogueLine("zhang_shiping", "dlg.lb1.zhang_shiping.N1.1"),
                new DialogueLine("zhang_shiping", "dlg.lb1.zhang_shiping.N1.2"), new DialogueLine("liu_bei", "dlg.lb1.zhang_shiping.N1.lb"),
                new DialogueLine("zhang_shiping", "dlg.lb1.zhang_shiping.N1.3"), new DialogueLine("su_shuang", "dlg.lb1.su_shuang.N1.2"),
            });
            // Only the wording differs; the same memory is written either way.
            yield return Ask("liu_bei", "dlg.lb1.zhuo_horse.ask", new[] { "dlg.lb1.zhuo_horse.why", "dlg.lb1.zhuo_horse.repay" });
            yield return lastChoice == 0 ? Say("zhang_shiping", "dlg.lb1.zhang_shiping.N1.why.1") : Say("su_shuang", "dlg.lb1.su_shuang.N1.repay.1");
            yield return Say("zhang_shiping", "dlg.lb1.zhang_shiping.N1.4");
            ch.Set(ZhuoGift);
            ch.Remember("zhang_shiping", "zhuo_gift", "accepted", "mem.zhuo_gift.zhang");
            ch.Remember("su_shuang", "zhuo_gift", "accepted", "mem.zhuo_gift.su");
        }

        IEnumerator SmithGift()
        {
            Face(Smith);
            yield return Lines(new[]
            {
                new DialogueLine("npc.N04", "dlg.lb1.npc.r1.zhuo.smith.N1.gift.1"), new DialogueLine("npc.N04", "dlg.lb1.npc.r1.zhuo.smith.N1.gift.2"),
                new DialogueLine("npc.N04", "dlg.lb1.npc.r1.zhuo.smith.N1.gift.3"), new DialogueLine("npc.N04", "dlg.lb1.npc.r1.zhuo.smith.N1.gift.4"),
                new DialogueLine("system", "dlg.lb1.zhuo_horse.got"),
            });
            Run.Chapter2.Set(ZhuoWeapons);
        }

        IEnumerator GuSou()
        {
            var ch = Run.Chapter2;
            Face(guSou);
            if (ch.Has(GuSouHeard)) { yield return Say("gu_sou", "dlg.lb1.gu_sou.N1.idle"); yield break; }
            yield return Lines(new[]
            {
                new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.1"), new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.2"),
                new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.3"), new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.4"),
                new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.5"), new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.6"),
                new DialogueLine("tavern_crowd", "dlg.lb1.gu_sou.N1.crowd"), new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.7"),
            });
            // Either answer leaves the next part (L1 ② at the Jingxing post station) open; only the greeting differs.
            yield return Ask("liu_bei", "dlg.lb1.gu_sou.N1.ask", new[] { "dlg.lb1.gu_sou.N1.introduce", "dlg.lb1.gu_sou.N1.coins" });
            if (lastChoice == 0)
            {
                yield return Lines(new[]
                {
                    new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.introduced.1"), new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.introduced.2"),
                    new DialogueLine("liu_bei", "dlg.lb1.gu_sou.N1.introduced.lb"), new DialogueLine("gu_sou", "dlg.lb1.gu_sou.N1.introduced.3"),
                });
                ch.Remember("gu_sou", "met_liu_bei", "introduced", "mem.gu_sou_met");
            }
            else
            {
                Run.Progress.Inventory.SpendCopper(5);
                yield return Say("gu_sou", "dlg.lb1.gu_sou.N1.anonymous.1");
                ch.Remember("gu_sou", "met_liu_bei", "anonymous", "mem.gu_sou_anonymous");
            }
            ch.Set(GuSouHeard);
        }

    }
}
