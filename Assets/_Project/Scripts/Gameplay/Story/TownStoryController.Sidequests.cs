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
    //   · sq.lb1.L4.zhuo 卢门：灵位前的字 (192–193, back in Zhuojun by the realm map): Lu Zhi has died in Shanggu; his son
    //     Lu Yu at the family's gate between the granary and the west gate, his widowed sister-in-law at the stove. Liu Bei
    //     may give three rations (memory lu_yu.rice=given), and, if he carries the stone-classic copy from Luoyang's
    //     ruins (R5b, flag lb1.L4.rubbing_got), leave it with Lu Yu or keep it (lu_yu.rubbing=gave_rubbing|kept_rubbing).
    // They stand in for the new Q sprites (Codex art/ch1-npc-sprites) as copies of townsfolk until those arrive.
    public sealed partial class TownStoryController
    {
        public const string ZhuoGift = "lb1.L5.zhuo_gift", ZhuoWeapons = "lb1.L5.zhuo_weapons", GuSouHeard = "lb1.L1.gu_sou_1";
        public const string LuMen = "lb1.L4.zhuo", LuMenHeard = "lb1.L4.zhuo_heard", RubbingGot = "lb1.L4.rubbing_got", LuzhiSlip = "lb1.L4.luzhi_slip";

        Transform zhangShiping, suShuang, guSou, luYu, luSao;
        bool hintedSmith;

        bool InZhuojunYears => Run.Chapter2.Step <= ChapterStep.ZhuojunFree;

        void SetUpSidequests()
        {
            int node = (int)Run.Chapter2.Node;
            if (node >= 6 && node <= 7)
            {
                // Between the granary (08) and the west gate (12), the doorway clear in front (open-space rule).
                luYu = StandIn(Resident("N10") ?? Resident("N09"), "lu_yu 卢毓", new Vector3(-40f, 0, 6f));
                luSao = StandIn(Resident("N08"), "lu_sao 卢家长嫂", new Vector3(-42.5f, 0, 8f));
                if (!Run.Chapter2.Has(LuMenHeard)) StartCoroutine(Hold(LuMenNews()));
            }
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
            if (Near(luYu, p)) return "side:lu_yu";
            if (Near(luSao, p)) return "side:lu_sao";
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
                case "side:lu_yu": StartCoroutine(Hold(LuYu())); return true;
                case "side:lu_sao": StartCoroutine(Hold(Say("lu_sao", (int)Run.Chapter2.Node >= 7 ? "dlg.lb1.lu_sao.N7.1" : "dlg.lb1.lu_sao.N6.1"))); return true;
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


        // ---------- 卢门：灵位前的字 ----------

        // The south gate guard's news on the first visit in these years (the design's trigger).
        IEnumerator LuMenNews()
        {
            yield return null;
            Run.Chapter2.Set(LuMenHeard);
            yield return Say("npc.N01", "dlg.lb1.L4.guard");
        }

        IEnumerator LuYu()
        {
            var ch = Run.Chapter2;
            Face(luYu);
            if ((int)ch.Node >= 7)
            {
                yield return Say("lu_yu", ch.Recall("lu_yu", "rice") == "given" ? "dlg.lb1.lu_yu.N7.rice" : "dlg.lb1.lu_yu.N7.1");
                yield break;
            }
            if (ch.Has(LuMen)) { yield return Say("lu_yu", "dlg.lb1.lu_yu.N6.4"); yield break; }
            yield return Lines(new[]
            {
                new DialogueLine("lu_yu", "dlg.lb1.lu_yu.N6.1"), new DialogueLine("lu_yu", "dlg.lb1.lu_yu.N6.2"),
                new DialogueLine("lu_yu", "dlg.lb1.lu_yu.N6.3"), new DialogueLine("lu_yu", "dlg.lb1.lu_yu.N6.4"),
            });
            // Rice: three rations from the bag; the family takes it only as the student's, and it costs standing (the design).
            var inv = Run.Progress.Inventory;
            if (inv.Rations >= 3)
            {
                yield return Ask("liu_bei", "dlg.lb1.L4.rice.ask", new[] { "dlg.lb1.L4.rice.give", "dlg.lb1.L4.rice.not" });
                if (lastChoice == 0)
                {
                    yield return Lines(new[] { new DialogueLine("lu_yu", "dlg.lb1.L4.rice.demur"), new DialogueLine("liu_bei", "dlg.lb1.L4.rice.named") });
                    inv.SpendRations(3);
                    int before = Run.Life.Reputation;
                    Run.Life.AddReputation(-2);
                    ch.Remember("lu_yu", "rice", "given", "mem.lu_yu.rice.given");
                    yield return ReputationChanged(Run.Life.Reputation - before);
                }
            }
            // The stone-classic copy from Luoyang's ruins, if Liu Bei carries it: Lu Yu knows the slip in it, not the stone.
            if (ch.Has(RubbingGot))
            {
                yield return Say("lu_yu", ch.Has(LuzhiSlip) ? "dlg.lb1.lu_yu.N6.rub.1" : "dlg.lb1.lu_yu.N6.rub.0");
                yield return Ask("liu_bei", "dlg.lb1.L4.rub.ask", new[] { "dlg.lb1.L4.rub.give", "dlg.lb1.L4.rub.keep" });
                int before = Run.Life.Reputation;
                if (lastChoice == 0)
                {
                    yield return Say("lu_yu", "dlg.lb1.lu_yu.N6.gave.1");
                    Run.Life.AddReputation(2);
                    ch.Remember("lu_yu", "rubbing", "gave_rubbing", "mem.lu_yu.rubbing.gave_rubbing");
                }
                else
                {
                    yield return Say("lu_yu", "dlg.lb1.lu_yu.N6.kept.1");
                    Run.Life.AddReputation(1);
                    ch.Remember("lu_yu", "rubbing", "kept_rubbing", "mem.lu_yu.rubbing.kept_rubbing");
                    yield return Say("system", "dlg.lb1.L4.rub.keepsake");
                }
                yield return ReputationChanged(Run.Life.Reputation - before);
            }
            ch.Set(LuMen);
        }
    }
}
