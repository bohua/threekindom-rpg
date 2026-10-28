namespace ThreeKingdoms.UI.Story
{
    // Words of the prologue 天下大势 (design/gdd/prologue.md §2, from CHAPTER1_PROLOGUE.md v0.1): a later historian's
    // voice, so 桓帝 / 灵帝 are allowed here and only here. Each shot's narration is a few subtitle lines shown in turn.
    public static class PrologueText
    {
        public const string Title = "序章 · 天下大势";
        public const string SkipHint = "确认／取消  跳过";
        public const string SkipQuestion = "跳过序章？";
        public const string SkipYes = "是", SkipNo = "否";
        public const string SkipNote = "跳过后可在行旅 · 史事中重看";

        public enum Form { Scroll, Map, Push, Scene }

        public readonly struct Shot
        {
            public readonly Form Form;
            public readonly string[] Lines;
            public Shot(Form form, params string[] lines) { Form = form; Lines = lines; }
        }

        public static readonly Shot[] Shots =
        {
            new Shot(Form.Scroll, "汉室四百年。高祖斩白蛇而起，光武中兴于洛阳。", "到了桓、灵之世，这四百年的气数，眼看要尽了。"),
            new Shot(Form.Scroll, "桓帝、灵帝信用宦官，两次党锢，天下士人禁锢终身；", "西园卖官，三公之位明码标价。", "朝廷把天下当作买卖，百姓就把朝廷当作仇人。"),
            new Shot(Form.Scroll, "连年大旱、蝗灾、瘟疫，流民塞路。", "这就是中平元年（184）春天的天下。"),
            new Shot(Form.Map, "巨鹿人张角，以符水治病，创太平道，十余年间徒众数十万，", "遍布青、徐、幽、冀、荆、扬、兖、豫八州。", "他们约定甲子年起事，口号是：", "“苍天已死，黄天当立，岁在甲子，天下大吉。”"),
            new Shot(Form.Map, "二月，八州同时举事，皆着黄巾。旬月之间，天下响应，京师震动。", "朝廷解除党锢，以何进为大将军守洛阳。"),
            new Shot(Form.Map, "朝廷派出三路大军：北中郎将卢植讨张角于冀州，皇甫嵩、朱儁讨颍川黄巾。", "又下诏各州郡自募义兵。", "这一纸诏书，让天下有志之人、有野心之人，都有了拿起刀的理由。"),
            new Shot(Form.Map, "这一年，骑都尉曹操三十岁，正随皇甫嵩南下颍川；", "下邳丞孙坚在淮泗募了千余人；", "河东太守董卓还在等一纸调令。", "他们彼此还不认识。"),
            new Shot(Form.Push, "天下纷乱，豪杰并起。", "而我们的故事，要从涿郡一个白身的青年说起。"),
            new Shot(Form.Scene, "中平元年春，涿郡城门的告示栏前，聚着几个看榜的百姓。", "人群里，那个白身的青年，正盯着那张榜文。"),
        };

        // The second narration (user 2026-10-28): the same lines in the half-classical voice of 《三国演义》, one for one
        // with Shots so the pictures keep their timing. Chosen in 史事 (GameSettings.NarrationStyle).
        public static readonly string[][] ClassicLines =
        {
            new[] { "话说汉室自高祖斩白蛇而起义，一统天下；后来光武中兴，定鼎洛阳。", "传至桓、灵二帝，四百年之气数，渐渐衰微。" },
            new[] { "桓帝、灵帝宠信宦官，两兴党锢之狱，天下名士尽遭禁锢；", "又于西园卖官鬻爵，三公之位，各有定价。", "朝廷视天下如市肆，百姓视朝廷如仇雠。" },
            new[] { "更兼连年大旱，蝗虫蔽天，疫疠流行，饥民塞道。", "此乃中平元年（184）春，天下之大势也。" },
            new[] { "却说巨鹿郡有一人，姓张名角，以符水为人治病，号太平道，十余年间，徒众数十万，", "青、徐、幽、冀、荆、扬、兖、豫八州之人，家家奉事。", "角遂与众约定甲子年起事，传言曰：", "“苍天已死，黄天当立，岁在甲子，天下大吉。”" },
            new[] { "二月，八州同日俱起，皆以黄巾裹头，旬月之间，天下响应，京师震动。", "朝廷乃解党锢，拜何进为大将军，屯兵以卫洛阳。" },
            new[] { "朝廷遣北中郎将卢植讨张角于冀州，左中郎将皇甫嵩、右中郎将朱儁讨颍川贼众。", "又诏各州郡自募义兵，以御贼寇。", "此诏一下，天下有志之士、怀野心之人，皆得持刀而起矣。" },
            new[] { "是年，骑都尉曹操，年方三十，正随皇甫嵩南下颍川；", "下邳丞孙坚，于淮泗之间募得千余人；", "河东太守董卓，尚候一纸调令。", "此数人者，彼时尚不相识。" },
            new[] { "正是：天下大乱，豪杰并起。", "今日且不说那王侯将相，单表涿郡一个白身之人。" },
            new[] { "时值中平元年春，涿郡城门之下，榜文高悬，百姓围看。", "那白身之人挤在人丛之中，正看那张榜文。" },
        };

        public const string StyleModern = "现代文", StyleClassic = "演义体";

        // The lines of a shot in the chosen voice.
        public static string[] LinesOf(int shot, bool classic) => classic ? ClassicLines[shot] : Shots[shot].Lines;

        // The pins of shots 4 and 7: who, the line under the name, and the map anchor they stand at (map_anchors).
        public readonly struct Pin
        {
            public readonly string Id, Name, Note, Anchor;
            public readonly float Dx, Dy;   // offset from the anchor, in 1920×1080 map pixels, so pins at one city do not overlap
            public Pin(string id, string name, string note, string anchor, float dx = 0, float dy = 0) { Id = id; Name = name; Note = note; Anchor = anchor; Dx = dx; Dy = dy; }
        }

        public static readonly Pin[] YellowTurbans =
        {
            new Pin("zhang_jiao", "张角", "大贤良师", "julu", 0, -110),
            new Pin("zhang_bao", "张宝", "地公将军", "julu", -120, -40),
            new Pin("zhang_liang", "张梁", "人公将军", "julu", 120, -40),
        };

        // 刘表 stands at 山阳 as a 党人: he was not in 荆州 until 190 (design/gdd/prologue.md §5). 公孙瓒's office in 184 is
        // not certain, so his pin only names 幽州.
        public static readonly Pin[] Heroes =
        {
            // Spread apart so no two overlap (user 2026-10-28: 董卓 sat on 曹操); a dot marks the city itself.
            new Pin("cao_cao", "曹操", "骑都尉 · 洛阳", "luoyang", -90, 120),
            new Pin("yuan_shao", "袁绍", "大将军幕下 · 洛阳", "luoyang", 95, 120),
            new Pin("sun_jian", "孙坚", "下邳丞", "xiapi", 95, -30),
            new Pin("dong_zhuo", "董卓", "河东太守", "hedong", -160, -30),
            new Pin("liu_biao", "刘表", "党人 · 山阳", "shanyang", 10, 110),
            new Pin("gongsun_zan", "公孙瓒", "幽州", "youzhou", 0, -30),
        };


        // 史事簿 (travel menu 史事): the prologue entry and its sources, opened one at a time on the right.
        public const string EntryTitle = "序章 · 天下大势", EntryWhen = "中平元年（184）春以前";
        public const string EntrySummary = "宦官当道，党锢禁绝士人，连年天灾，流民塞路。张角以太平道聚众，八州并起；朝廷遣三路大军，又诏州郡自募义兵。涿郡告示栏前，一个白身的青年正在看那张榜文。";
        public static readonly (string Title, string Text)[] Sources =
        {
            ("党锢之祸", "桓帝延熹九年（166）、灵帝建宁二年（169）两次党锢，士人禁锢终身，黄巾起后解除；光和元年（178）开西园卖官。《后汉书》党锢列传、灵帝纪。"),
            ("太平道", "张角奉黄老道，以符水咒说疗病，十余年间徒众数十万，连结青、徐、幽、冀、荆、扬、兖、豫八州。“苍天已死，黄天当立”见《后汉书·皇甫嵩传》。"),
            ("三路官军", "中平元年，北中郎将卢植讨张角于冀州，左中郎将皇甫嵩、右中郎将朱儁讨颍川黄巾。《后汉书》卢植、皇甫嵩传。"),
            ("募义兵榜文〔演义〕", "幽州刘焉出榜招军见《三国演义》第一回；史书中刘焉此时尚非幽州牧，刘备从校尉邹靖讨黄巾为史实。"),
        };
    }
}
