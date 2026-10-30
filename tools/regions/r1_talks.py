"""R1 涿蓟 wild-spot people, after design/chapter1-rise/npc-sidequests/R1_R2_zhuoji_zhongshan.md §R1.3 and the
§R1.6 wiring table (v0.2, 2026-10-29). Writes each place's "talks" into hubs_R1.json: who stands where, what they say
from which chronicle node (N1 184, N4 190, N5 191, N6 192, N7 193, N8 194), with the design's dialogue keys.
Lines are copied from the design; only the people and ambient lines, no side-quest steps (beacon, reed mat and the
empty tent are backfill-layer cards). Run before prepare_region.py."""
import json, os, sys


def E(frm, speaker, lines, keys, until=0):
    e = {'from': frm, 'speaker': speaker, 'lines': lines, 'keys': keys}
    if until:
        e['until'] = until
    return e


def T(name, sprite, at, *entries, frame=0):
    return dict(name=name, sprite=sprite, at=at, frame=frame, entries=list(entries))


TALKS = {
    'ferry': [
        T('渡翁易老', 'N09', [4, 7],
          E(4, '渡翁易老', ['荆卿走的就是这条水。燕地的人送人，不说“早回”，只唱这一句。'], ['dlg.lb1.npc.r1.ferry.yi.N4.1']),
          E(6, '渡翁易老', ['这几年往南渡的都是兵，往北渡的都是伤兵。'], ['dlg.lb1.npc.r1.ferry.yi.N6.1'])),
    ],
    'hunter': [
        T('猎户石大', 'N27', [-4, -8],
          E(4, '猎户石大', ['皮子要不要？狐皮、獾皮。山上的马蹄印，有钉掌的是汉马，没钉的是胡马。'], ['dlg.lb1.npc.r1.hunt.shi.N4.1']),
          E(6, '猎户石大', ['白马营的人来山里找走散的马。找回三匹，两匹带着箭。'], ['dlg.lb1.npc.r1.hunt.shi.N6.1'])),
        T('狐皮', None, [-5, -3],
          E(4, '', ['门口挂着一张狐皮。石大今年打的第三张。'], ['dlg.lb1.npc.r1.hunt.pelt'])),
    ],
    'beacon': [
        T('郝老卒', 'N01', [0, -7],
          E(4, '郝老卒', ['老卒郝某，奉令守烽燧，十二年。烽燧废了三年，没人来换岗，老卒不敢走。',
                         '烽燧不点，是好事。可烽燧不点，就没人记得上面还有人。'],
            ['dlg.lb1.beacon.s1.0', 'dlg.lb1.beacon.s1.1'], until=5)),
        T('冷灰与竹签', None, [2, -6],
          E(6, '', ['灰是冷的。竹签上刻着十二道，最后一道刻得很浅。'], ['dlg.lb1.npc.r1.beacon.empty.1'])),
    ],
    'tomb': [
        T('守墓人哑叔', 'N30', [5, -8],
          E(4, '', ['他指指古墓，又指指天，意思是：比汉还老。'], ['dlg.lb1.npc.r1.tomb.keeper.N4']),
          E(5, '', ['他在古墓前扫出一块空地，不知要给谁。'], ['dlg.lb1.npc.r1.tomb.keeper.N5']),
          E(7, '', ['他指向古墓东侧一座新坟，坟前插着一块没有字的木牌。'], ['dlg.lb1.npc.r1.tomb.keeper.N7'])),
        T('古碑', None, [0, -4.5],
          E(4, '', ['碑字磨平了，只剩一个“燕”字。'], ['dlg.lb1.npc.r1.tomb.stele.1'])),
    ],
    'wood_bridge': [
        T('胡篾匠', 'N25', [25, 3],
          E(4, '胡篾匠', ['这桥是柳湾和东陌两村合钱修的。过桥不要钱，帮着扛一捆苇子就行。'], ['dlg.lb1.npc.r1.bridge.hu.N4.1']),
          E(6, '胡篾匠', ['桥上过兵，桥板压断了两块。村里出人修的，官里不管。'], ['dlg.lb1.npc.r1.bridge.hu.N6.1'])),
        T('割苇少年', 'N22', [27, -5],
          E(4, '割苇少年', ['别踩那边的黑泥！湿土陷脚，陷进去鞋就没了！'], ['dlg.lb1.npc.r1.river.kids.N4']),
          E(5, '割苇少年', ['沙滩上的苇子最直，织席最好。'], ['dlg.lb1.npc.r1.river.kids.N5']),
          E(7, '割苇少年', ['我哥当兵去了，苇子我一个人割。'], ['dlg.lb1.npc.r1.river.kids.N7'])),
    ],
    'post1': [
        T('孟驿丞', 'N28', [0, -14],
          E(4, '孟驿丞', ['往北蓟城，往南涿县，往西就是关了——关那边，您过不去。'], ['dlg.lb1.npc.r1.zj.station.N4.1']),
          E(7, '孟驿丞', ['蓟城那边乱了几天。驿马给公孙将军征走一半，您要用马，得等。'], ['dlg.lb1.npc.r1.zj.station.N7.1'])),
        T('卖浆吴婆', 'N26', [12, -16],
          E(4, '卖浆吴婆', ['喝碗浆再走。往西那条别去了，关上不放人。'], ['dlg.lb1.npc.r1.signpost.wu.N4.1']),
          E(7, '卖浆吴婆', ['这几天往北跑的都是州里的人，往南跑的也是。'], ['dlg.lb1.npc.r1.signpost.wu.N7.1'])),
        T('路口路标', None, [15, -12],
          E(1, '', ['北至蓟　南至涿　西至居庸关　东南往广阳　西南往易'], ['dlg.lb1.npc.r1.signpost.read'])),
    ],
    'post2': [
        T('关前驿驿卒', 'N07', [0, -8],
          E(4, '关前驿驿卒', ['往关上去的信，十封有九封退回来。'], ['dlg.lb1.npc.r1.gq.station.N4']),
          E(5, '关前驿驿卒', ['卢尚书家里人来问过路，我只能说过不去。'], ['dlg.lb1.npc.r1.gq.station.N5']),
          E(7, '关前驿驿卒', ['这驿站，就剩我一个了。'], ['dlg.lb1.npc.r1.gq.station.N7'])),
    ],
    'checkpoint': [
        T('杜关吏', 'N03', [0, -3],
          E(4, '杜关吏', ['站住。居庸关以北是上谷，关外乌桓、鲜卑游骑出没，边塞戒严。',
                         '没有州里的出塞文书，谁也不放。刘使君发的文书，我这儿一年也见不到几张。',
                         '你问关外？草长起来的时候，马蹄声能传十里。听见了，就得点烽火。'],
            ['dlg.lb1.npc.r1.juyong.official.N4.1', 'dlg.lb1.npc.r1.juyong.official.N4.2', 'dlg.lb1.npc.r1.juyong.official.N4.3']),
          E(6, '杜关吏', ['卢尚书在上谷过世，他家里人想出关收殓，我也没放。……令就是令。'], ['dlg.lb1.npc.r1.juyong.official.N6.1']),
          E(7, '杜关吏', ['州里换了主人，出塞令还是那道令。关，不会为谁开。'], ['dlg.lb1.npc.r1.juyong.official.N7.1'])),
    ],
    'juyong': [
        T('关门', None, [0, -7],
          E(1, '', ['关门紧闭。城楼上的戍卒朝你摇头。'], ['dlg.lb1.npc.r1.juyong.gate.1'])),
    ],
    'shrine': [
        T('桑婆', 'N08', [3, -6],
          E(4, '桑婆', ['这树的叶子，养活了半村的蚕。贵人不贵人老身不懂，蚕是真吃饱了。'], ['dlg.lb1.npc.r1.ls.shrine.caifeng.1'])),
    ],
    'camp': [
        T('放马老牧人老郭', 'N12', [6, -28],
          E(1, '老郭', ['这片草场？没人驻。听说公孙家的伯珪在外头做官，养了一群白马。哪天他来了，这草就不够吃了。'],
            ['dlg.lb1.npc.r1.gz.herder.N1.1'], until=3)),
        T('营门哨兵', 'N01', [5, -26],
          E(4, '营门哨兵', ['公孙将军营。报名号，下马步行。'], ['dlg.lb1.npc.r1.gz.gate.N4.1'])),
    ],
    'willow_village': [
        T('柳湾村村正', 'N09', [0, -12],
          E(4, '柳湾村村正', ['柳湾二十户，户户割苇织席。苇子好不好，看拒马河的水。'], ['dlg.lb1.npc.r1.lw.headman.N4']),
          E(5, '柳湾村村正', ['去年水大，沙滩淹了一半。'], ['dlg.lb1.npc.r1.lw.headman.N5']),
          E(7, '柳湾村村正', ['征了三个后生去当兵。葛翁的孙女现在顶一个劳力。'], ['dlg.lb1.npc.r1.lw.headman.N7'])),
    ],
    'east_village': [
        T('郝家孙儿', 'N10', [0, -11],
          E(4, '郝家孙儿', ['我爷爷在山上守烽火，我没见过他。'], ['dlg.lb1.npc.r1.dm.grandson.N4']),
          E(7, '郝家孙儿', ['爷爷说，烽火不点是好事。'], ['dlg.lb1.npc.r1.dm.grandson.N7'])),
    ],
    'lousang': [
        T('楼桑村童', 'N23', [14, 10],
          E(4, '楼桑村童', ['我要乘羽葆盖车！', '你乘个屁，你连驴都没骑过！'], ['dlg.lb1.npc.r1.ls.kids.N4.1', 'dlg.lb1.npc.r1.ls.kids.N4.2']),
          E(5, '楼桑村童', ['刘家大郎回来了！他真骑马了！'], ['dlg.lb1.npc.r1.ls.kids.N5']),
          E(7, '楼桑村童', ['大人说，桑树底下不许乱说话。为什么？'], ['dlg.lb1.npc.r1.ls.kids.N7'])),
    ],
    'exit_0': [T('路牌', None, [0, -2], E(1, '', ['往广阳。'], ['dlg.lb1.npc.r1.edge.guangyang']))],
    'exit_1': [T('路牌', None, [0, -2], E(1, '', ['往易县。前面是冀州地界。'], ['dlg.lb1.npc.r1.edge.yi']))],
}

f = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), 'hubs_R1.json')
h = json.load(open(f, encoding='utf-8'))
for k, v in TALKS.items():
    h.setdefault(k, {})['talks'] = v
open(f, 'w', encoding='utf-8').write(json.dumps(h, ensure_ascii=False, indent=1))
print('talks', sum(len(v) for v in TALKS.values()), 'at', len(TALKS), 'places')
