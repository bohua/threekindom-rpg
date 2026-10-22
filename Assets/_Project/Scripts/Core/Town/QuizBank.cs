using System;
using System.Linq;

namespace ThreeKingdoms.Core.Town
{
    // The scholar's questions (user 2026-10-22): facts from 《三国演义》, first option right as written here; the
    // options are shuffled when asked. Answers follow the Romance (e.g. 双股剑, 卖酒屠猪), not the historical record.
    public static class QuizBank
    {
        public readonly struct Question
        {
            public readonly string Text;
            public readonly string[] Options;   // [0] is right
            public Question(string text, params string[] options) { Text = text; Options = options; }
        }

        public static readonly Question[] All =
        {
            new Question("桃园结义的三人是？", "刘备、关羽、张飞", "刘备、赵云、张飞", "曹操、夏侯惇、夏侯渊"),
            new Question("关羽所用的兵器是？", "青龙偃月刀", "丈八蛇矛", "方天画戟"),
            new Question("张飞所用的兵器是？", "丈八蛇矛", "青龙偃月刀", "古锭刀"),
            new Question("演义中刘备起兵时所用的兵器是？", "双股剑", "方天画戟", "青釭剑"),
            new Question("黄巾起义的首领是？", "张角", "程远志", "张梁"),
            new Question("张角自号什么将军？", "天公将军", "地公将军", "人公将军"),
            new Question("张宝自号什么将军？", "地公将军", "天公将军", "人公将军"),
            new Question("张梁自号什么将军？", "人公将军", "地公将军", "天公将军"),
            new Question("「苍天已死」的下一句是？", "黄天当立", "岁在甲子", "天下大吉"),
            new Question("刘备，字什么？", "玄德", "云长", "翼德"),
            new Question("关羽，字什么？", "云长", "子龙", "孟德"),
            new Question("张飞，字什么？", "翼德", "公瑾", "子敬"),
            new Question("刘备自称是谁的后人？", "中山靖王刘胜", "汉高祖刘邦", "光武帝刘秀"),
            new Question("演义中刘备少时以何为业？", "贩屦织席", "卖酒屠猪", "推车贩枣"),
            new Question("演义中张飞在涿郡以何为业？", "卖酒屠猪", "贩屦织席", "打铁铸剑"),
            new Question("关羽是哪里人？", "河东解良", "涿郡", "常山真定"),
            new Question("赵云是哪里人？", "常山真定", "河东解良", "燕人"),
            new Question("「不求同年同月同日生」的下一句是？", "但愿同年同月同日死", "只求同心协力共进退", "愿为兄弟共富贵"),
            new Question("演义第一回中，关羽阵前斩杀的黄巾将领是？", "程远志", "邓茂", "张宝"),
            new Question("演义第一回中，张飞一矛刺死的黄巾副将是？", "邓茂", "程远志", "张梁"),
            new Question("刘关张最初投效的幽州太守是？", "刘焉", "公孙瓒", "陶谦"),
            new Question("刘备「三顾茅庐」请出的是？", "诸葛亮", "庞统", "徐庶"),
            new Question("「温酒斩华雄」的是哪位将军？", "关羽", "张飞", "吕布"),
            new Question("吕布的坐骑叫什么？", "赤兔马", "的卢", "绝影"),
            new Question("刘备「跃马过檀溪」骑的是哪匹马？", "的卢", "赤兔马", "爪黄飞电"),
            new Question("「三让徐州」是谁把徐州让给刘备？", "陶谦", "吕布", "刘表"),
            new Question("长坂坡单骑救阿斗的是？", "赵云", "张飞", "关羽"),
            new Question("张飞据水断桥、一声大喝退曹军，是在哪座桥？", "长坂桥", "灞陵桥", "洛阳桥"),
            new Question("「三英战吕布」发生在哪里？", "虎牢关", "汜水关", "下邳城"),
            new Question("曹操，字什么？", "孟德", "仲谋", "本初"),
            new Question("诸葛亮，字什么？", "孔明", "士元", "元直"),
        };

        // A question as asked: options shuffled, and the index of the right one among them.
        public static (string text, string[] options, int answer) Ask(int index, Random rng)
        {
            var q = All[index];
            var order = Enumerable.Range(0, q.Options.Length).OrderBy(_ => rng.Next()).ToArray();
            return (q.Text, order.Select(i => q.Options[i]).ToArray(), Array.IndexOf(order, 0));
        }
    }
}
