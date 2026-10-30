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
            ["liu_yu"] = "刘虞", ["ji_gate_clerk"] = "秦门吏", ["ji_inn_keeper"] = "魏掌柜", ["ji_guest_camp"] = "楼上营里的客",
            ["ji_guest_state"] = "楼下州里的客", ["ji_refugee"] = "许氏", ["ji_east_gate"] = "通事老达", ["kumo"] = "库莫",
            ["guan_jing"] = "关靖", ["liu_bei_mu"] = "刘母", ["liu_zijing"] = "刘子敬", ["liu_yuanqi"] = "刘元起", ["yuanqi_qi"] = "元起妻",
            ["liu_deran"] = "刘德然", ["young_liu_bei"] = "少年刘备", ["young_liu_zijing"] = "刘子敬（当年）",
            ["lu_yu"] = "卢毓", ["lu_sao"] = "卢家长嫂",
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

            // ---------- sq.lb1.r1.jian_yong_ear 简雍打听 (蓟城, 190) ----------
            ["dlg.lb1.jian_yong.N4.1"] = "州治就是不一样，酒楼里坐的人，一半在骂公孙将军，一半在夸他。",
            ["dlg.lb1.jian_yong_ear.s0.1"] = "玄德，你去见刘幽州，雍去酒楼和市集转一圈。半个时辰后，州府台阶下见。",
            ["dlg.lb1.jian_yong_ear.wait"] = "燕台酒楼、市集粥棚、东便门，你也去听听。听完了，州府台阶下见。",   // 补
            ["dlg.lb1.npc.r1.ji.inn_keeper.N4.1"] = "客官楼上请。今日楼上坐的是营里的，楼下坐的是州里的，您挑一边。",
            ["dlg.lb1.jian_yong_ear.s1.camp"] = "公孙将军见胡人就打，痛快！",
            ["dlg.lb1.jian_yong_ear.s1.state"] = "打了胡人，明年谁来卖马？",
            ["dlg.lb1.npc.r1.ji.refugee.N4"] = "俺们从齐地一路走来，刘使君给分了田。俺这辈子没见过这么宽的官。",
            ["dlg.lb1.npc.r1.ji.east_gate.N4"] = "胡人进城卖马，走这个门。他们叫刘使君‘好官’，乌桓话怎么说，我教你。",
            ["dlg.lb1.npc.r1.ji.east_gate.N5"] = "白马营的人从这门过，胡人就躲。",
            ["dlg.lb1.npc.r1.ji.gate_clerk.N4.1"] = "刘使君在台阶上，您直接过去就是。他不让我们拦百姓。",
            ["dlg.lb1.jian_yong_ear.s4.1"] = "一座城里两种话。一种说宽了没用，一种说打了没完。雍都记下了，你挑哪个信？",
            ["dlg.lb1.jian_yong_ear.s4.lb"] = "都信。说话的人，都是真心那么想。",
            ["dlg.lb1.jian_yong_ear.s4.2"] = "这就对了。天下的事，就是这么一句一句攒起来的。",
            ["dlg.lb1.jian_yong_ear.unlocked"] = "简雍记下两条传闻：“刘幽州宽厚”“公孙将军好战”。天下传闻可随时翻看。",
            ["mem.jian_yong_first_rumor"] = "简雍记得：初到蓟城，替玄德记下了一座城里的两种话",
            // 刘虞 (剧本第四节 5)
            ["dlg.lb1.liu_yu.N4.1"] = "涿县刘玄德？卢子幹的学生。上来说话，台阶下风大。",
            ["dlg.lb1.liu_yu.N4.ask"] = "敢问使君，幽州百姓近来如何？",
            ["dlg.lb1.liu_yu.N4.2"] = "去年谷价，一石三十钱。青、徐逃来的人，在这里分到了田。",
            ["dlg.lb1.liu_yu.N4.3"] = "有人劝虞多练兵，有人劝虞出塞。兵可一时，民不可一时。",
            ["dlg.lb1.liu_yu.N4.4"] = "你在伯珪营中，替虞看着些。他的白马跑得快，有时会跑过头。",
            ["dlg.lb1.liu_yu.N4.idle"] = "胡人来蓟城卖马，是虞许的。他们有马，我们有盐铁布帛。换着用，就不用抢。",
            ["mem.liu_yu_asked_people"] = "刘虞记得：刘玄德在州府台阶下，问的是幽州的百姓",

            // ---------- sq.lb1.r1.hu_market 胡市马契 ----------
            ["dlg.lb1.npc.r1.ji.wuhuan_trader.N4.1"] = "库莫，乌桓人。刘公给的符，看。马，好马，换盐，换布。",
            ["dlg.lb1.hu_market.s1.1"] = "刘公说，你们有盐，我们有马，换着用，就不用抢。营里的人，不听刘公的。",
            ["dlg.lb1.npc.r1.ji.wuhuan_trader.N4.2"] = "营里的人，拉走我两匹马。说乌桓人的马，是贼马。不是贼马！",
            ["dlg.lb1.hu_market.goal"] = "库莫的两匹马扣在公孙瓒营。去营里军需处问问。",   // 补
            ["dlg.lb1.npc.r1.gz.quarter.N4.2"] = "乌桓人的马？将军说了，胡人都是贼。我扣两匹，有什么不对？",
            ["dlg.lb1.hu_market.ask"] = "（怎么把马要回来？）",
            ["dlg.lb1.hu_market.opt_camp"] = "走营里的规矩：找长史关靖",
            ["dlg.lb1.hu_market.opt_liu_yu"] = "走州里的规矩：回州府请刘幽州",
            ["dlg.lb1.hu_market.to_guan_jing"] = "长史关靖在军需处门口。",   // 补
            ["dlg.lb1.hu_market.to_liu_yu"] = "刘幽州在蓟城州府台阶上。",   // 补
            ["dlg.lb1.hu_market.camp_rule.0"] = "扣错了就退。可营里的规矩是：退马要有人担保，赔这两匹马的草料钱。",
            ["dlg.lb1.hu_market.camp_rule.1"] = "靖只认规矩。规矩说扣错了要退，也说退了要有人担。",
            ["dlg.lb1.hu_market.poor"] = "草料钱要六十文，眼下凑不齐。",   // 补
            ["dlg.lb1.hu_market.horses"] = "刘备出了六十文担保。两匹马牵出营门，送回蓟城东便门。",   // 补
            ["dlg.lb1.hu_market.camp_cold"] = "刘君下次有事，先来找靖。",
            ["dlg.lb1.hu_market.liu_yu.1"] = "伯珪的兵扣虞的符……这样的事，以后还会有。",
            ["dlg.lb1.hu_market.writ"] = "门吏持刘虞手书到营。郑军需不情愿地退了马，嘟囔“州里的手伸到营里来了”。",
            ["dlg.lb1.hu_market.end.1"] = "马契，分两半。一半库莫的，一半你的。以后乌桓人看见这半片，就知道你是讲理的汉人。",
            ["dlg.lb1.hu_market.keepsake"] = "得到信物“胡市马契”——半片刻木契，库莫所赠。",
            ["mem.guan_jing_paid_for_hu"] = "关靖记得：刘玄德拿自己的钱护胡人",
            ["mem.liu_yu_sent_writ"] = "刘虞记得：为乌桓人的两匹马，写过一纸手书到营里",
            ["dlg.lb1.guan_jing.N4.1"] = "长史关靖。营里的规矩：马归马厩，粮归军需，人归名册。刘君的人也要入册。",
            ["dlg.lb1.guan_jing.N4.2"] = "将军待你好，是将军的事。靖只认名册。",

            // ---------- sq.lb1.L6.lousang 旧地（一）：桑下 ----------
            ["dlg.lb1.liu_bei_mu.N4.1"] = "回来了？……瘦了。席子还会织么？手生了吧。",
            ["dlg.lb1.liu_bei_mu.N4.2"] = "你小时候坐不住，织三行就往树底下跑。现在倒坐得住了。",
            ["dlg.lb1.liu_bei_mu.N4.3"] = "外面的事娘不问。你只记着，鞋要穿合脚的，饭要吃热的。",
            ["dlg.lb1.liu_zijing.N4.1"] = "玄德。你来看这棵树？它还那样，像个车盖。",
            ["dlg.lb1.liu_zijing.N4.2"] = "你小时候在这底下说，要乘羽葆盖车。老夫骂你：汝勿妄语，灭吾门也。",
            ["dlg.lb1.L6.ask"] = "（怎么回答叔父？）",
            ["dlg.lb1.L6.was_wrong"] = "叔父骂得对。那时备是妄语。",
            ["dlg.lb1.L6.never_forgot"] = "备记得那句话，一直没忘。",
            ["dlg.lb1.liu_zijing.N4.was_wrong.1"] = "……那时是。现在老夫不知道了。",
            ["dlg.lb1.liu_zijing.N4.never_forgot.1"] = "记着好。记着，才知道那句话能灭门，也就知道不能乱说。",
            ["dlg.lb1.liu_zijing.N4.2b"] = "去吧。树老夫看着，门也老夫看着。",
            ["mem.liu_zijing_was_wrong"] = "刘子敬记得：玄德说，当年那句是妄语",
            ["mem.liu_zijing_never_forgot"] = "刘子敬记得：玄德说，那句话他一直没忘",
            ["dlg.lb1.liu_yuanqi.N4.1"] = "玄德！快进来。德然天天念叨你，说你在外面带兵了。",
            ["dlg.lb1.liu_yuanqi.N4.2"] = "当年我说，吾宗中有此儿，非常人也。我那婆娘不信。你说，我看准了没有？",
            ["dlg.lb1.yuanqi_qi.N4.1"] = "……当年那句话，我是说过。各自一家，何能常尔。",
            ["dlg.lb1.yuanqi_qi.N4.lb"] = "婶娘说的是实话。那几年备吃的是刘家的米。",
            ["dlg.lb1.yuanqi_qi.N4.2"] = "哼，知道就好。……锅里有饼，拿两个路上吃。",
            ["dlg.lb1.liu_deran.N4.1"] = "玄德。卢先生门下，我们三个，就我没出息，在这儿教娃娃识字。",
            ["dlg.lb1.liu_deran.N4.2"] = "你那时候不爱读书，喜欢狗马、音乐、好衣裳。先生讲经，你在后头看窗外。",
            ["dlg.lb1.liu_deran.N4.3"] = "可先生问话，你总答得出。我到现在也没想明白。",
            ["dlg.lb1.L6.sight"] = "……还是那么大。",
            ["dlg.lb1.L6.flash.0"] = "（树下，一群孩子在玩。最小的那个爬上树根，指着头顶的树冠。）",   // 补
            ["dlg.lb1.L6.flash.1"] = "吾必当乘此羽葆盖车！",
            ["dlg.lb1.L6.flash.2"] = "汝勿妄语，灭吾门也！",
            ["dlg.lb1.L6.flash.3"] = "（风过桑林。刘备在树下站了一会儿。）",   // 补
            ["mem.liu_bei_L6_lousang"] = "刘备记得：回到楼桑村，在那棵桑树下站了一会儿",

            // ---------- sq.lb1.L4.zhuo 卢门：灵位前的字 (涿郡, 192–193) ----------
            ["dlg.lb1.L4.guard"] = "卢尚书家挂白了。",
            ["dlg.lb1.lu_yu.N6.1"] = "您是……刘玄德？家父的学生。请进，只是家里没有好茶。",
            ["dlg.lb1.lu_yu.N6.2"] = "家父在上谷山中过世。遗命葬于土穴，不用棺椁，只裹一层单帛。",
            ["dlg.lb1.lu_yu.N6.3"] = "关外的路不通，我们去不了。只能在堂上设个灵位。",
            ["dlg.lb1.lu_yu.N6.4"] = "两位兄长都殁了。嫂嫂带着侄儿，家里只剩毓一个男丁。毓今年十岁。",
            ["dlg.lb1.lu_sao.N6.1"] = "小叔每天先分粥，分完了才吃。他吃的那一碗，最稀。",
            ["dlg.lb1.lu_sao.N7.1"] = "刘君，孩子面前我不哭。您也别说可怜的话，他听不得。",
            ["dlg.lb1.L4.rice.ask"] = "（行囊里还有军粮。）",
            ["dlg.lb1.L4.rice.give"] = "留下三份军粮",
            ["dlg.lb1.L4.rice.not"] = "什么也不说",
            ["dlg.lb1.L4.rice.demur"] = "家父说，不受无名之食。",
            ["dlg.lb1.L4.rice.named"] = "这是学生给先生家的，有名。",
            ["dlg.lb1.lu_yu.N6.rub.1"] = "这片简……是家父批过的。‘之’字最后一笔往上挑。是家父的字。",
            ["dlg.lb1.lu_yu.N6.rub.0"] = "这是太学的石经？家父说过，他在东观校过这些经。",
            ["dlg.lb1.L4.rub.ask"] = "（石经摹本……）",
            ["dlg.lb1.L4.rub.give"] = "留给卢毓",
            ["dlg.lb1.L4.rub.keep"] = "自己留着",
            ["dlg.lb1.lu_yu.N6.gave.1"] = "毓会把它挂在灵位旁边。等毓读懂了这一段，再告诉您。",
            ["dlg.lb1.lu_yu.N6.kept.1"] = "您留着好。家父校过的东西，在您那里，就还在外面走着。",
            ["dlg.lb1.L4.rub.keepsake"] = "信物“石经摹本”留在身边——绑卢植。",
            ["dlg.lb1.lu_yu.N7.1"] = "今年北边又打起来了，米价一斗……毓记不清了，反正是买不起。",
            ["dlg.lb1.lu_yu.N7.rice"] = "上回您送的粮，嫂嫂省着吃到了开春。毓记着。",
            ["mem.lu_yu.rice.given"] = "卢毓记得：家父过世那年，刘玄德留下了三份军粮",
            ["mem.lu_yu.rubbing.gave_rubbing"] = "卢毓记得：刘玄德把石经摹本留在了家父的灵位旁",
            ["mem.lu_yu.rubbing.kept_rubbing"] = "卢毓记得：家父校过的东西，在刘玄德那里还走着",
        };
    }
}
