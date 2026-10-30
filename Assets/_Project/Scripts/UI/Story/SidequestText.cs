using System.Collections.Generic;

namespace ThreeKingdoms.UI.Story
{
    // Chapter 1 NPC and side-quest lines (design/chapter1-rise/npc-sidequests, v0.2 confirmed by the user 2026-10-29;
    // core layer only). Keys are the design's dialogue keys (dlg.lb1.…), so the text can move to the localisation tables
    // as it stands. Lines are copied from the design; a few short lines marked 补 fill steps the design describes in
    // prose without a line.
    public static class SidequestText
    {
        public static readonly Dictionary<string, string> Speakers = new Dictionary<string, string>
        {
            ["zhang_shiping"] = "张世平", ["su_shuang"] = "苏双", ["gu_sou"] = "鼓叟", ["tavern_crowd"] = "酒客",
        };

        public static readonly Dictionary<string, string> Lines = new Dictionary<string, string>
        {
            // ---------- sq.lb1.L5.zhuo_horse 中山马商（一）：不是买卖 (R1_R2 §R1.2 / §R1.4) ----------
            ["dlg.lb1.su_shuang.N1.1"] = "张兄，就是他——带乡勇出城的刘家大郎。",
            ["dlg.lb1.zhang_shiping.N1.1"] = "中山张世平，这是苏双。我们贩马，年年要来涿郡几趟。",
            ["dlg.lb1.zhang_shiping.N1.2"] = "前日城外那一仗，我们在驿楼上看了。几百乡勇，没一个先跑的。",
            ["dlg.lb1.zhang_shiping.N1.lb"] = "是乡亲们肯拼命，备不敢居功。",
            ["dlg.lb1.zhang_shiping.N1.3"] = "这话我信一半。肯拼命的人，也要看跟着谁拼。",
            ["dlg.lb1.su_shuang.N1.2"] = "张兄，别绕了。刘君，我们想出一笔钱，买兵器、买马，随你用。",
            ["dlg.lb1.zhuo_horse.ask"] = "（怎么回答？）",
            ["dlg.lb1.zhuo_horse.why"] = "二位与备素不相识，为何如此？",
            ["dlg.lb1.zhuo_horse.repay"] = "备眼下无以为报。",
            ["dlg.lb1.zhang_shiping.N1.why.1"] = "正因素不相识才出。认识了就是人情，人情要还。我们不图这个。",
            ["dlg.lb1.su_shuang.N1.repay.1"] = "谁要你报？我们看马看了半辈子，看人也就看个大概。",
            ["dlg.lb1.zhang_shiping.N1.4"] = "钱我们已经交给城里铁匠老韩了。去看看他给你们打什么吧。",
            ["dlg.lb1.su_shuang.N1.idle"] = "下回见面，怕是在中山了。到中山，我请你喝酒——不许推。",
            ["mem.zhuo_gift.zhang"] = "张世平记得：涿郡驿站门前，出钱资助刘家兄弟买兵器、买马",
            ["mem.zhuo_gift.su"] = "苏双记得：涿郡驿站门前，出钱资助刘家兄弟买兵器、买马",
            // 铁匠老韩 npc.r1.zhuo.N04_smith — the three weapons (剧本 §10.1 #1; names and weights 演义第 1 回)
            ["dlg.lb1.npc.r1.zhuo.smith.N1.gift.1"] = "张、苏二位的钱到了，镔铁我也有。就看你们兄弟三个要什么。",
            ["dlg.lb1.npc.r1.zhuo.smith.N1.gift.2"] = "两把剑，一般长，一般重。刘家大郎说，左手右手都得用得上。",
            ["dlg.lb1.npc.r1.zhuo.smith.N1.gift.3"] = "红脸那位说，刀要重，重了才劈得开阵。八十二斤，我抡了三天锤。",
            ["dlg.lb1.npc.r1.zhuo.smith.N1.gift.4"] = "黑脸那位说要长，越长越好。一丈八，再长就得两个人抬了。",
            ["dlg.lb1.zhuo_horse.got"] = "得到双股剑、青龙偃月刀、丈八蛇矛的来历。——张世平、苏双出资，铁匠老韩打造。",
            ["dlg.lb1.zhuo_horse.hint"] = "铁匠老韩有东西要交给你们。",

            // ---------- sq.lb1.L1.gu_sou_1 黄天当立 (鼓叟 N1, at the tavern after battle 3) ----------
            ["dlg.lb1.gu_sou.N1.1"] = "列位——今儿不说古，说个新鲜的！",
            ["dlg.lb1.gu_sou.N1.2"] = "巨鹿有个张角，号大贤良师。徒众分三十六方，大方万余人，小方六七千。",
            ["dlg.lb1.gu_sou.N1.3"] = "他们喊的是：‘苍天已死，黄天当立，岁在甲子，天下大吉。’一喊，八州一齐动了。",
            ["dlg.lb1.gu_sou.N1.4"] = "有人说他画符烧灰，兑水给人喝，病就好了。老汉不说灵不灵，只说一句：喝的人多，饿的人更多。",
            ["dlg.lb1.gu_sou.N1.5"] = "朝廷三路出兵，各州募义兵。这回在涿县城外把黄巾挡住的，是三个人。",
            ["dlg.lb1.gu_sou.N1.6"] = "一个卖草鞋的，一个卖豆的，一个杀猪的。——列位别笑。",
            ["dlg.lb1.gu_sou.N1.crowd"] = "哈哈哈！鼓叟你又编！卖草鞋的能打黄巾？",
            ["dlg.lb1.gu_sou.N1.7"] = "笑吧笑吧。老汉说的都是真事——只是还没人信罢了。",
            ["dlg.lb1.gu_sou.N1.ask"] = "（散场了。上前说一句？）",
            ["dlg.lb1.gu_sou.N1.introduce"] = "老丈说的那织席的，就是备。",
            ["dlg.lb1.gu_sou.N1.coins"] = "（只在鼓面上放几枚钱）",
            ["dlg.lb1.gu_sou.N1.introduced.1"] = "……老汉说了四十年书，头一回说到正主脸上。",
            ["dlg.lb1.gu_sou.N1.introduced.2"] = "那老汉斗胆问一句：这书往下，该怎么说？",
            ["dlg.lb1.gu_sou.N1.introduced.lb"] = "往下的事，备自己还不知道。",
            ["dlg.lb1.gu_sou.N1.introduced.3"] = "好！那老汉就跟着看。看完了，再说。",
            ["dlg.lb1.gu_sou.N1.anonymous.1"] = "这位客官出手大方。老汉下回开书，给你留个前排。",
            ["dlg.lb1.gu_sou.N1.idle"] = "张角那边的书老汉还得往下打听。听说卢中郎将在广宗围着他呢。",
            ["mem.gu_sou_met"] = "鼓叟记得：涿县酒馆里，说书说到了正主",
            ["mem.gu_sou_anonymous"] = "鼓叟记得：涿县酒馆里，有位客官在鼓面上放了钱",
        };
    }
}
