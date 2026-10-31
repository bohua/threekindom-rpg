# -*- coding: utf-8 -*-
"""《三国·江山如画》战斗与兵书数值模型（第一篇章）
纯函数：属性→派生、伤害、克制、资源、AT 推后、八卦盘点亮。
参数集 "v1" = 现状（V1_SPEC §5 + 原型代码 2026-10-26），"v2" = 本稿建议值。
"""
from dataclasses import dataclass, field
from typing import Dict, List, Tuple, Optional
import math

# ---------------- 参数集 ----------------
PARAMS = {
    "v1": dict(
        hp_base=80, hp_per_lv=20, atk_per_lv=2, def_per_lv=1, mat_per_lv=2, mdef_per_lv=1,
        max_attr=100,
        speed={"inf": 6, "cav": 10, "arc": 8, "reb": 6, "str": 7},
        at_mode="v1",                      # delay = cost*8/speed
        ep_mode="v1",                      # EP = (4+2L)*4, 无回复
        cp_mode="v1",                      # 命中 +8 / 受击 +5, 开局 0
        troop_adv=0.4, elem_adv=0.2, adv_cap=0.6, min_taken=0.3,
        overflow_step=0.08, overflow_cap=5,
        cord_by="level_int",               # 绳长按当前智力
        tech_big_req=4, sweep_power=0.65, zhangjiao_elem="土",
        growth="v1", cp_start=30, cp_craft_factor=1.0, interrupt=False, elem_override={}, cost_override={}, surplus_cast=False,
    ),
    "v2": dict(
        hp_base=80, hp_per_lv=16, atk_per_lv=4, def_per_lv=2, mat_per_lv=4, mdef_per_lv=2,   # 用户 10-27：四维不成长，成长全在等级项
        max_attr=100,
        speed={"inf": 6, "cav": 10, "arc": 8, "reb": 6, "str": 7},
        at_mode="v2",                      # delay = cost*16/(speed+10)
        ep_mode="v2",                      # EP = 20+智力//2, 每次行动 +智力//20（E3）
        cp_mode="v2",                      # 命中 +(5+武力//10)（武技命中减半，E2）, 受击 +6, 策略命中 +4, 击破 +10, 开局 30（同现码）
        troop_adv=0.3, elem_adv=0.2, adv_cap=0.6, min_taken=0.3,   # 用户 10-27：大兵种压制 +0.3 ＋ 兵种特长
        overflow_step=0.08, overflow_cap=5,
        troop_v2=True,
        troop_override={"刘备": "inf", "张飞": "cav", "白马义从": "harc", "麴义": "xbow", "强弩手": "xbow", "先登死士": "spear",
                        "曹军步卒": "spear", "于禁": "inf", "张宝": "str", "张梁": "reb", "山贼头目": "reb", "山贼喽啰": "reb",
                        "流寇头目": "reb", "流寇": "reb"},
        cord_by="base_int",                # 智力固定后与 level_int 等价
        tech_big_req=3, sweep_power=0.8, zhangjiao_elem="土",        # D3：张角保留土
        tier1_overflow_cap=2, tier1_power={"引火": 1.1},
        growth="v2", cp_start=30, cp_craft_factor=0.5, interrupt=True,
        elem_override={"黄巾将领": "木", "张角亲卫": "木", "黄巾余部": "木",   # D3：首领土、道徒木
                       "山贼头目": "金", "山贼喽啰": "金", "流寇头目": "金", "流寇": "金", "文丑": "金", "袁军骑": "金", "强弩手": "金",
                       "麴义": "木", "先登死士": "木"},   # T5：按武将表标注
        cost_override={"抚军": 8},                                      # E3
        surplus_cast=True,                                              # E5：封顶以上每点溢出换咏唱 −5 AT（上限 3 点）
        # 用户 10-27：四维按三国志尺度固定；蜀汉核心超群，曹魏二流各有所长（武/统/智）
        attr_override={
            "刘备": (68, 78, 76), "关羽": (96, 88, 70), "张飞": (97, 78, 40), "赵云": (93, 86, 74),
            "吕布": (100, 88, 25), "文丑": (92, 72, 30), "麴义": (78, 86, 60), "管亥": (82, 62, 22),
            "曹仁": (82, 88, 60), "于禁": (76, 86, 68), "张宝": (70, 62, 55), "张梁": (72, 60, 48),
            "程远志": (62, 55, 30), "山贼头目": (70, 50, 25), "流寇头目": (72, 55, 28),
        },
    ),
    # 消融：只改 AT 推后公式
    "v1_at": None,
}

PARAMS["v1_at"] = dict(PARAMS["v1"]); PARAMS["v1_at"]["at_mode"] = "v2"

# 兵种：inf 步兵(刀盾) spear 枪兵 cav 骑兵 arc 弓兵 xbow 弩兵 harc 弓骑 str 军师 reb 贼众
TROOP_BONUS = {"inf": (30, 5), "spear": (20, 3), "cav": (10, 0), "arc": (0, 0), "xbow": (10, 2), "harc": (0, 0), "reb": (20, 0), "str": (0, 0)}  # (兵力, 物防)
TROOP_SPEED = {"inf": 6, "spear": 6, "cav": 10, "arc": 8, "xbow": 7, "harc": 10, "reb": 6, "str": 7}
TROOP_MOVE = {"inf": 3, "spear": 3, "cav": 5, "arc": 3, "xbow": 3, "harc": 5, "reb": 3, "str": 3}
TROOP_RANGE = {"inf": 1, "spear": 1, "cav": 1, "arc": 3, "xbow": 2, "harc": 2, "reb": 1, "str": 2}
BIG = {"inf": "步", "spear": "步", "reb": "步", "cav": "骑", "harc": "骑", "arc": "弓", "xbow": "弓", "str": "军"}
# 大兵种压制：骑压步、步压弓、弓压骑（v1：骑克步/黄巾，步克弓，弓克骑）
def troop_beats(a, b, P=None):
    if P and P.get("troop_v2"):
        A, B = BIG[a], BIG[b]
        return (A, B) in (("骑", "步"), ("步", "弓"), ("弓", "骑"))
    return (a == "cav" and b in ("inf", "reb")) or (a == "inf" and b == "arc") or (a == "arc" and b == "cav")
# 兵种特长（v2）：攻方对守方的附加倍率（可正可负）
def troop_trait(a, b, moved=False, melee=True):
    x = 0.0
    if a == "spear" and BIG[b] == "骑": x += 0.3      # 枪兵拒马：对骑 +0.3
    if b == "spear" and BIG[a] == "骑": x -= 0.4      # 骑冲枪阵：压制被抵消再减（净 0.9，T4）
    if a == "xbow" and BIG[b] == "骑": x += 0.2       # 强弩破骑
    if BIG[a] == "弓" and BIG[b] == "骑": x += 0.1    # 弓压骑再 +0.1（净 1.4，T3）
    if BIG[a] == "步" and melee and b in ("arc", "harc", "str"): x += 0.2   # 步系近战贴脸弓/军师 +0.2（不给骑系，T3）
    if BIG[a] == "骑" and moved: x += 0.1             # 骑兵冲锋：移动后攻击
    return x

ELEMS = ["金", "木", "水", "火", "土"]
OVERCOMES = {"木": "土", "土": "水", "水": "火", "火": "金", "金": "木"}
GENERATES = {"木": "火", "火": "土", "土": "金", "金": "水", "水": "木"}

EXP_TABLE = [0, 10, 30, 60, 100, 150, 210, 280, 370, 480, 610, 760, 930, 1120, 1330]  # 1..15 级

def level_for_exp(exp):
    lv = 1
    for i, e in enumerate(EXP_TABLE):
        if exp >= e: lv = i + 1
    return lv

@dataclass
class General:
    id: str; name: str; troop: str
    force: int; command: int; intellect: int
    g_force: int = 0; g_cmd: int = 0; g_int: int = 0
    school: str = "兵"        # 儒水 兵火 道木 墨金 法土
    element: Optional[str] = None   # 单位五行；None → 按学派偏向
    crafts: List[str] = field(default_factory=list)   # 绝学名
    floor: float = 0.0        # 不可击败首领的兵力下限比例

SCHOOL_ELEM = {"儒": "水", "兵": "火", "道": "木", "墨": "金", "法": "土"}

# v2 成长表：用户 2026-10-27 定案——武/统/智原生四维固定不随等级成长，成长只在派生值的等级项
GROWTH_V2 = {"刘备": (0, 0, 0), "关羽": (0, 0, 0), "张飞": (0, 0, 0), "赵云": (0, 0, 0)}

def troop_of(g, P):
    return P.get("troop_override", {}).get(g.name, g.troop)

def growth_of(g, P):
    if P.get("growth", "v1") == "v2" and g.name in GROWTH_V2: return GROWTH_V2[g.name]
    return (g.g_force, g.g_cmd, g.g_int)

def attr(base, growth, level, P):
    return min(P["max_attr"], base + growth * (level - 1))

@dataclass
class Stats:
    force: int; command: int; intellect: int
    hp: int; atk: int; dfn: int; mat: int; mdef: int; speed: int; tech: int

def compute(g: General, level: int, P, bonus=None) -> Stats:
    b = bonus or {}
    gf, gc, gi = growth_of(g, P)
    bf, bc, bi = P.get("attr_override", {}).get(g.name, (g.force, g.command, g.intellect))
    f = attr(bf, gf, level, P); c = attr(bc, gc, level, P); i = attr(bi, gi, level, P)
    troop = troop_of(g, P)
    tb, db = TROOP_BONUS[troop]
    return Stats(
        force=f, command=c, intellect=i,
        hp=P["hp_base"] + P["hp_per_lv"] * level + tb + b.get("hp", 0),
        atk=10 + f // 2 + P["atk_per_lv"] * (level - 1) + b.get("atk", 0),
        dfn=5 + c // 3 + P["def_per_lv"] * (level - 1) + db + b.get("def", 0),
        mat=10 + i // 2 + P["mat_per_lv"] * (level - 1) + b.get("mat", 0),
        mdef=5 + i // 3 + P["mdef_per_lv"] * (level - 1) + b.get("mdef", 0),
        speed=(TROOP_SPEED[troop] if P.get("troop_v2") else P["speed"][troop]) + b.get("speed", 0),
        tech=4 + 2 * level,
    )

# ---------------- 资源 ----------------
def ep_max(st: Stats, P, troop=None):
    base = st.tech * 4 if P["ep_mode"] == "v1" else 20 + st.intellect // 2
    return base + (10 if (troop == "str" and P.get("troop_v2")) else 0)   # 军师 +10

def ep_regen(st: Stats, P):
    return 0 if P["ep_mode"] == "v1" else st.intellect // 20

def cp_on_hit(st: Stats, P, craft=False, troop=None):
    base = 8 if P["cp_mode"] == "v1" else 5 + st.force // 10
    if troop == "reb" and P.get("troop_v2"): base += 2   # 贼众悍勇
    return int(base * P.get("cp_craft_factor", 1.0)) if craft else base

def cp_on_taken(P):
    return 5 if P["cp_mode"] == "v1" else 6

def cp_on_cast(P):
    return 0 if P["cp_mode"] == "v1" else 4

def cp_on_kill(P):
    return 0 if P["cp_mode"] == "v1" else 10

def cp_start(P):
    return P.get("cp_start", 30)

# ---------------- AT ----------------
def at_delay(cost, speed, P):
    if P["at_mode"] == "v1":
        return cost * 8.0 / max(1, speed)
    return cost * 16.0 / (speed + 10)

AT_COST = dict(attack=100, craft=110, move=70, move_extra=30, guard=50, wait=40, recover=80)

# ---------------- 伤害 ----------------
def adv_mul(a_troop, t_troop, skill_elem, t_elem, P, moved=False, troop=True, melee=True):
    adv = 0.0
    if troop:   # 策略不吃兵种克制（T1，BattleCoreTests 既有规则）
        if troop_beats(a_troop, t_troop, P): adv += P["troop_adv"]
        if P.get("troop_v2"): adv += troop_trait(a_troop, t_troop, moved, melee)
    if skill_elem and t_elem and OVERCOMES.get(skill_elem) == t_elem: adv += P["elem_adv"]
    return 1.0 + max(-0.3, min(adv, P["adv_cap"]))

def damage(atk, dfn, coef, adv, taken=1.0, height=1.0, atk_mul=1.0, def_mul=1.0, P=None):
    base = atk * atk_mul * coef - dfn * def_mul * 0.5
    return max(1, math.floor(base * adv * height * taken))

# ---------------- 技能 ----------------
@dataclass
class Skill:
    name: str; kind: str          # "craft"(绝学/武技) | "strategy"
    effect: str                   # damage heal guard buff debuff
    area: str                     # single circle line selfcircle
    rng: int; radius: int = 0; length: int = 0; width: int = 1
    power: float = 1.0; might: bool = True
    cost: int = 0; cast: int = 0; recover: int = 80; at_delay: int = 0
    turns: int = 0; amount: float = 0.0
    elem: Optional[str] = None
    req: Dict[str, int] = field(default_factory=dict)
    def main_elem(self):
        return max(self.req, key=self.req.get) if self.req else self.elem

# 绝学（人物自带，0.2–0.5 现值，TacticsGame.Chapter.cs）
CRAFTS = {
    "整队": Skill("整队", "craft", "heal", "single", 3, power=0.30, might=False, cost=20),
    "火矢": Skill("火矢", "craft", "damage", "single", 4, power=1.3, might=False, cost=25),
    "突击": Skill("突击", "craft", "damage", "single", -1, power=1.5, might=True, cost=25),
    "破阵": Skill("破阵", "craft", "debuff", "single", -1, power=1.1, might=True, cost=30, turns=2, amount=0.2),
    "横扫": Skill("横扫", "craft", "damage", "selfcircle", 0, radius=2, power=0.65, might=True, cost=30),
    "威慑": Skill("威慑", "craft", "debuff", "selfcircle", 0, radius=3, power=0.0, might=True, cost=25, turns=2, amount=0.2),
    "援护": Skill("援护", "craft", "guard", "selfcircle", 0, radius=1, power=0, cost=20, turns=1, amount=0.3),
    "连击": Skill("连击", "craft", "damage", "single", -1, power=1.5, might=True, cost=25),   # 两段 0.75
    "天下无双": Skill("天下无双", "craft", "damage", "line", 1, length=2, power=1.3, might=True, cost=0),
    "引火(绝学)": Skill("引火", "strategy", "damage", "single", 5, power=1.25, might=False, cost=6, cast=40),
}

def S(name, elem, main, sec, secreq, effect, area, rng, radius, power, cost, cast, length=0, width=1, at_delay=0, turns=0, amount=0.0, might=False):
    sk = Skill(name, "strategy", effect, area, rng, radius, length, width, power, might, cost, cast, 80, at_delay, turns, amount, elem)
    sk.req[elem] = main
    if sec: sk.req[sec] = secreq
    return sk

# 策略表（原型现值 14.3 / TacticsRules.cs）
STRATEGIES = [
    S("引火", "火", 2, None, 0, "damage", "single", 5, 0, 1.25, 6, 40),
    S("火烧营寨", "火", 4, "木", 1, "damage", "circle", 5, 1, 1.0, 12, 70),
    S("火海", "火", 7, "木", 2, "damage", "circle", 6, 2, 1.1, 22, 110),
    S("截流", "水", 2, None, 0, "damage", "single", 5, 0, 1.0, 6, 40, at_delay=30),
    S("水攻", "水", 4, "金", 1, "damage", "line", 1, 0, 0.9, 12, 70, length=5, at_delay=25),
    S("决堤", "水", 7, "金", 2, "damage", "line", 1, 0, 1.05, 22, 110, length=7, width=3, at_delay=45),
    S("抚军", "木", 2, None, 0, "heal", "single", 4, 0, 0.30, 6, 30),
    S("鼓舞", "木", 4, "水", 1, "heal", "circle", 4, 1, 0.20, 12, 60, turns=2, amount=0.2),
    S("收拢溃卒", "木", 7, "水", 2, "heal", "circle", 0, 99, 0.40, 22, 100),
    S("坚壁", "土", 2, None, 0, "guard", "single", 4, 0, 0.0, 6, 30, turns=2, amount=0.25),
    S("八阵", "土", 6, "火", 2, "guard", "circle", 3, 2, 0.0, 20, 90, turns=3, amount=0.35),
    S("箭雨", "金", 3, None, 0, "damage", "circle", 6, 1, 0.85, 10, 60, might=True),
    S("万箭齐发", "金", 6, "土", 2, "damage", "circle", 6, 2, 1.0, 20, 100, might=True),
]
STRAT_BY_NAME = {s.name: s for s in STRATEGIES}

def T(name, elem, effect, area, rng, radius, power, cost, turns=0, amount=0.0, length=0, at_delay=0):
    return Skill(name, "craft", effect, area, rng, radius, length, 1, power, True, cost, 0, 110, at_delay, turns, amount, elem)

TECH_SMALL = {
    "金": T("刺击", "金", "damage", "single", -1, 0, 1.3, 20),
    "火": T("猛击", "火", "damage", "single", -1, 0, 1.2, 18),
    "水": T("闪避", "水", "guard", "selfcircle", 0, 0, 0, 15, turns=1, amount=0.4),
    "木": T("调息", "木", "heal", "selfcircle", 0, 0, 0.15, 20),
    "土": T("格挡", "土", "guard", "selfcircle", 0, 0, 0, 15, turns=1, amount=0.35),
}
TECH_BIG = {
    "金": T("突阵", "金", "damage", "line", 1, 0, 1.1, 35, length=3),
    "火": T("疾风骤雨", "火", "damage", "single", -1, 0, 1.8, 35),
    "水": T("迂回", "水", "damage", "single", -1, 0, 1.4, 30, at_delay=25),
    "木": T("死战", "木", "heal", "selfcircle", 0, 0, 0.20, 35, turns=2, amount=0.3),
    "土": T("中流砥柱", "土", "guard", "selfcircle", 0, 1, 0, 35, turns=2, amount=0.35),
}

# ---------------- 八卦盘（TacticsRules.cs Circuits / CircuitMath 的移植） ----------------
@dataclass
class Book:
    name: str; elem: str; grade: int; kind: str = "策略"   # 策略 | 功法
    @property
    def is_tech(self): return self.kind == "功法"

# 卦位：乾0 兑1 离2 震3 巽4 坎5 艮6 坤7；槽 0 为中枢
GUA_ELEM = ["金", "金", "火", "木", "木", "水", "土", "土"]
GUA_NAME = ["乾", "兑", "离", "震", "巽", "坎", "艮", "坤"]

@dataclass
class Circuit:
    school: str; affinity: str
    slots: List[Tuple[int, Optional[str]]]   # (卦位 或 -1 中枢, 锁定五行)
    lines: List[List[int]]

CIRCUITS = {
    "儒": Circuit("儒", "水", [(-1, "水"), (5, None), (0, None), (1, None), (2, None), (4, None), (3, None)], [[0, 1, 2, 3], [4, 5, 6]]),
    "兵": Circuit("兵", "火", [(-1, "火"), (6, None), (3, None), (4, "火"), (2, "火"), (7, None)], [[0, 1, 2, 3, 4, 5]]),
    "道": Circuit("道", "木", [(-1, "木"), (4, None), (2, None), (1, None), (0, None), (5, None), (6, None)], [[0, 1, 2], [3, 4], [5, 6]]),
    "墨": Circuit("墨", "金", [(-1, "金"), (1, "金"), (0, None), (5, None), (7, "土"), (2, None)], [[0, 1, 2, 3], [4, 5]]),
    "法": Circuit("法", "土", [(-1, "土"), (7, "土"), (1, "金"), (0, None), (5, None), (6, "土")], [[0, 1, 2, 3], [5, 4]]),
}

CAP_TABLE = [(0, 2), (40, 3), (60, 4), (75, 5), (90, 6), (98, 99)]
def segment_cap(intel):
    cap = 2
    for m, c in CAP_TABLE:
        if intel >= m: cap = c
    return cap

def segments(c: Circuit, intel):
    cap = segment_cap(intel)
    if cap >= len(c.slots):
        return [list(range(len(c.slots)))]
    out = []
    for cord in c.lines:
        for start in range(0, len(cord), cap):
            out.append(cord[start:start + cap])
    return out

def book_value(c: Circuit, b: Book):
    return b.grade + (1 if b.elem == c.affinity else 0)

def line_totals(c, slots, line):
    t = {e: 0 for e in ELEMS}
    for i in line:
        b = slots[i] if i < len(slots) else None
        if b and not b.is_tech: t[b.elem] += book_value(c, b)
    return t

import dataclasses
def strategy_def(s, P, surplus=0):
    pw = P.get("tier1_power", {}); co = P.get("cost_override", {})
    kw = {}
    if s.name in pw: kw["power"] = pw[s.name]
    if s.name in co: kw["cost"] = co[s.name]
    if P.get("surplus_cast") and surplus > 0: kw["cast"] = max(10, s.cast - 5 * min(surplus, 3))
    return dataclasses.replace(s, **kw) if kw else s

def lit_strategies(c, slots, intel, P):
    res = []
    totals = [line_totals(c, slots, l) for l in segments(c, intel)]
    for s in STRATEGIES:
        best, best_over = -1, -1
        for li, t in enumerate(totals):
            if all(t[k] >= v for k, v in s.req.items()):
                over = t[s.main_elem()] - s.req[s.main_elem()]
                if over > best_over: best, best_over = li, over
        if best >= 0:
            cap = P["overflow_cap"]
            if s.req[s.main_elem()] <= 3 and "tier1_overflow_cap" in P: cap = P["tier1_overflow_cap"]
            res.append((strategy_def(s, P, max(0, best_over - cap)), best, best_over, 1 + P["overflow_step"] * min(best_over, cap)))
    return res

def lit_techniques(c, slots, intel, P):
    res = []
    for li, seg in enumerate(segments(c, intel)):
        run = []
        def flush():
            req = P.get("tech_big_req", 4)
            if P.get("tech_big_req", 4) >= 4:
                groups = [run[k:k + 3] for k in range(0, len(run), 3)]          # 现规则：任意相邻 2–3 本，取主五行
            else:
                groups, cur = [], []                                            # 建议规则：相邻且同五行才成组
                for b in run:
                    if cur and (b.elem != cur[-1].elem or len(cur) == 3): groups.append(cur); cur = []
                    cur.append(b)
                if cur: groups.append(cur)
            for part in groups:
                tot = {}
                for b in part: tot[b.elem] = tot.get(b.elem, 0) + book_value(c, b)
                main = max(tot, key=tot.get)
                if len(part) >= 2 and tot[main] >= req:
                    res.append((TECH_BIG[main], li, tot[main] - req, 1 + P["overflow_step"] * min(tot[main] - req, P["overflow_cap"])))
                else:
                    for b in part: res.append((TECH_SMALL[b.elem], li, 0, 1.0))
            run.clear()
        for i in seg:
            b = slots[i] if i < len(slots) else None
            if b and b.is_tech: run.append(b)
            else: flush()
        flush()
    best = {}
    for r in res:
        if r[0].name not in best or r[2] > best[r[0].name][2]: best[r[0].name] = r
    return list(best.values())

# 书库与开局镶嵌（Bookcase.cs）
def library():
    L = []
    def B(n, e, g): L.append(Book(n, e, g))
    def Tq(n, e, g): L.append(Book(n, e, g, "功法"))
    B("《尉缭子》", "金", 2); B("《吴子》", "金", 3); B("《六韬·虎韬》", "金", 1); B("《尉缭子》残卷", "金", 1)
    B("《三略》", "木", 2); B("《司马法》", "木", 2); B("《管子·牧民》", "木", 1); B("《司马法》残卷", "木", 1)
    B("《管子·度地》", "水", 3); B("《墨子·备水》", "水", 2); B("《孙子·行军》", "水", 1)
    B("《孙子·火攻》", "火", 3); B("《六韬·火战》", "火", 2); B("《火攻》残卷", "火", 1); B("《火攻》残卷", "火", 1)
    B("《孙膑兵法·八阵》", "土", 3); B("《墨子·备城门》", "土", 2); B("《六韬·龙韬》", "土", 1)
    Tq("《剑道》", "金", 2); Tq("《剑道》残卷", "金", 1); Tq("《李将军射法》", "金", 1)
    Tq("《手搏》", "火", 2); Tq("《手搏》残卷", "火", 1)
    Tq("《越女剑》", "水", 2); Tq("《导引》", "木", 2); Tq("《盾法》", "土", 2)
    Tq("《盾法》残卷", "土", 1)   # 建议新增（v2）
    return L

def default_slots(name, lib, P=None):
    def L(n): return next(b for b in lib if b.name == n)
    s = [None] * 7
    if P is not None and P.get("growth") == "v2":      # E4：默认书降一品，3 品撤出开局
        if name == "刘备":   # 儒：中枢—坎—乾—兑｜离—巽—震
            s[0] = L("《墨子·备水》"); s[1] = L("《孙子·行军》"); s[2] = L("《三略》"); s[3] = L("《司马法》"); s[6] = L("《李将军射法》")
        elif name == "关羽": # 兵：中枢—艮—震—巽(火)—离(火)—坤
            s[0] = L("《六韬·火战》"); s[3] = L("《火攻》残卷"); s[4] = L("《火攻》残卷"); s[1] = L("《尉缭子》残卷"); s[2] = L("《管子·牧民》"); s[5] = L("《剑道》")
        elif name == "张飞": # 法：中枢—坤(土)—兑(金)—乾｜艮(土)—坎
            s[0] = L("《墨子·备城门》"); s[1] = L("《六韬·龙韬》"); s[2] = L("《尉缭子》"); s[3] = L("《手搏》"); s[5] = L("《盾法》"); s[4] = L("《盾法》残卷")
        return s
    if name == "刘备":
        s[0] = L("《管子·度地》"); s[1] = L("《三略》"); s[2] = L("《司马法》"); s[3] = L("《墨子·备水》"); s[6] = L("《李将军射法》")
    elif name == "关羽":
        s[0] = L("《孙子·火攻》"); s[3] = L("《六韬·火战》"); s[4] = L("《火攻》残卷"); s[1] = L("《尉缭子》"); s[2] = L("《管子·牧民》"); s[5] = L("《剑道》")
    elif name == "张飞":
        s[0] = L("《孙膑兵法·八阵》"); s[1] = L("《墨子·备城门》"); s[2] = L("《吴子》"); s[5] = L("《盾法》"); s[4] = L("《手搏》")
    return s

# ---------------- 人物与敌人数据（Assets/_Project/Data/Characters） ----------------
GENERALS = {
    "刘备": General("liu_bei", "刘备", "arc", 65, 75, 75, 1, 2, 2, "儒", crafts=["整队", "火矢"]),
    "关羽": General("guan_yu", "关羽", "cav", 90, 80, 60, 2, 2, 1, "兵", crafts=["突击", "破阵"]),
    "张飞": General("zhang_fei", "张飞", "inf", 95, 75, 45, 3, 2, 1, "法", crafts=["横扫", "威慑"]),
    "赵云": General("zhao_yun", "赵云", "cav", 88, 85, 70, 2, 2, 2, "兵", crafts=["援护", "连击"]),
    # 敌方（成长 0）
    "程远志": General("cheng_yuanzhi", "程远志", "reb", 60, 55, 30, school="法"),
    "杂兵头领": General("yt_headman", "杂兵头领", "reb", 40, 40, 20, school="法"),
    "黄巾将领": General("yt_officer", "黄巾将领", "reb", 55, 45, 30, school="法"),
    "张梁": General("zhang_liang", "张梁", "inf", 75, 65, 50, school="法"),
    "张宝": General("zhang_bao", "张宝", "inf", 70, 65, 60, school="法"),
    "张角": General("zhang_jiao", "张角", "str", 40, 70, 85, school="道", element="土", crafts=["引火(绝学)"]),
    "张角亲卫": General("yt_guard", "张角亲卫", "reb", 60, 50, 30, school="法"),
    "董卓本阵": General("dong_zhuo", "董卓本阵", "inf", 70, 40, 50, school="兵"),
    "山贼头目": General("bandit_chief", "山贼头目", "inf", 72, 55, 30, school="兵"),
    "山贼喽啰": General("bandit", "山贼喽啰", "inf", 45, 40, 20, school="兵"),
    "白马义从": General("baima_rider", "白马义从", "cav", 72, 62, 40, school="兵"),
    "吕布": General("lu_bu", "吕布", "cav", 100, 85, 30, school="兵", crafts=["天下无双"], floor=0.5),
    "并州骑": General("bingzhou_cavalry", "并州骑", "cav", 66, 55, 25, school="兵"),
    "流寇头目": General("liukou_chief", "流寇头目", "inf", 74, 58, 30, school="兵"),
    "流寇": General("liukou", "流寇", "inf", 52, 44, 20, school="兵"),
    "文丑": General("wen_chou", "文丑", "cav", 94, 78, 40, school="兵", floor=0.45),
    "袁军骑": General("yuan_cavalry", "袁军骑", "cav", 66, 56, 25, school="兵"),
    "麴义": General("qu_yi", "麴义", "inf", 82, 90, 62, school="兵", floor=0.45),
    "先登死士": General("xiandeng", "先登死士", "inf", 72, 76, 20, school="兵"),
    "强弩手": General("qiangnu", "强弩手", "arc", 70, 50, 30, school="兵"),
    "管亥": General("guan_hai", "管亥", "reb", 86, 66, 30, school="法"),
    "黄巾余部": General("yt_remnant", "黄巾余部", "reb", 64, 52, 24, school="法"),
    "曹军步卒": General("cao_infantry", "曹军步卒", "inf", 70, 64, 30, school="兵"),
    "曹军弓手": General("cao_archer", "曹军弓手", "arc", 66, 56, 34, school="兵"),
    "曹仁": General("cao_ren", "曹仁", "cav", 90, 86, 60, school="兵", floor=0.45),
    "曹军骑兵": General("cao_cavalry", "曹军骑兵", "cav", 72, 60, 28, school="兵"),
    "散兵": General("deserter", "散兵", "inf", 66, 54, 24, school="兵"),
    "于禁": General("yu_jin", "于禁", "inf", 84, 90, 70, school="兵", floor=0.45),
}

def unit_element(g: General, P=None):
    if P and g.name in P.get("elem_override", {}): return P["elem_override"][g.name]
    return g.element or SCHOOL_ELEM[g.school]

# 第一篇章战斗表（Assets/_Project/Data/Encounters；战棋另加两名末位兵种小卒 等级−1）
# (战斗id, 名称, 我方累计经验(战前), 敌方[(名, 等级)], 本战经验, 备注)
# 经验为“主线不选做”路线（E1）；0.5 四战经验改 170/190/210/140（主线 b15 后 1340＝15 级，全做 b12 后 930＝13 级，无死区）
BATTLES = [
    ("b1", "程远志来犯", 0, [("程远志", 2), ("杂兵头领", 1), ("杂兵头领", 1)], 10, "回合制"),
    ("b2", "张梁", 10, [("张梁", 3), ("黄巾将领", 1), ("黄巾将领", 1), ("黄巾将领", 1)], 20, "回合制"),
    ("b3", "张角三兄弟", 30, [("张角", 4), ("张宝", 4), ("张梁", 4)], 30, "回合制"),
    ("b4", "颍川夜战", 60, [("张宝", 5), ("张梁", 5), ("黄巾将领", 3), ("黄巾将领", 3), ("黄巾将领", 2), ("黄巾将领", 2)], 40, "第3回合火起敌混乱、第6回合曹操援军"),
    ("b5", "广宗救董卓", 100, [("张角", 6), ("张角亲卫", 4), ("张角亲卫", 4), ("张角亲卫", 4), ("张角亲卫", 3), ("张角亲卫", 3)], 50, "护卫董卓；张角30%退走"),
    ("b6", "安喜剿匪", 150, [("山贼头目", 6), ("山贼喽啰", 4), ("山贼喽啰", 4), ("山贼喽啰", 4), ("山贼喽啰", 3), ("山贼喽啰", 3)], 60, "奖励战"),
    ("b7", "校场演武(选做)", 210, [("白马义从", 7), ("白马义从", 7), ("白马义从", 6), ("白马义从", 5), ("白马义从", 5)], 70, "选做"),
    ("b8", "虎牢关三英战吕布", 210, [("吕布", 9), ("并州骑", 7), ("并州骑", 7), ("并州骑", 6), ("并州骑", 6)], 90, "吕布Floor0.5守关"),
    ("b9", "平原御寇", 300, [("流寇头目", 10), ("流寇", 9), ("流寇", 9), ("流寇", 9), ("流寇", 9), ("流寇", 8), ("流寇", 8)], 90, "3人"),
    ("b10", "磐河救公孙瓒", 390, [("文丑", 12), ("袁军骑", 11), ("袁军骑", 11), ("袁军骑", 11), ("袁军骑", 10), ("袁军骑", 10)], 110, "文丑Floor0.45；赵云客将"),
    ("b11", "界桥殿后", 500, [("麴义", 12), ("先登死士", 11), ("先登死士", 11), ("强弩手", 11), ("强弩手", 11), ("强弩手", 10), ("强弩手", 10)], 130, "麴义Floor0.45"),
    ("b12", "都昌解围", 630, [("管亥", 13), ("黄巾余部", 12), ("黄巾余部", 12), ("黄巾余部", 11), ("黄巾余部", 11), ("黄巾余部", 10), ("黄巾余部", 10)], 170, "5选3+后备"),
    ("b13", "下邳城东", 800, [("曹军步卒", 12), ("曹军步卒", 12), ("曹军弓手", 12), ("曹军弓手", 11), ("曹军弓手", 11)], 190, ""),
    ("b14", "下邳保卫", 990, [("曹仁", 14), ("曹军骑兵", 13), ("曹军骑兵", 13), ("曹军骑兵", 12), ("曹军骑兵", 12)], 210, "曹仁Floor0.45"),
    ("b15", "小沛巡防(选做)", 1200, [("散兵", 14), ("散兵", 13), ("流寇", 13), ("流寇", 13), ("散兵", 13), ("散兵", 12), ("散兵", 12)], 140, "选做"),
]
PARTY4 = ("b10", "b11", "b12", "b13", "b14", "b15")   # 赵云 0.4 磐河起客将

# 建议阵容（v2）：敌军等级/数量按“难度指数预算”调整；未列出的战斗沿用原阵容
BATTLES_V2_OVERRIDES = {
    "b4": [("张宝", 5), ("张梁", 5), ("黄巾将领", 2), ("黄巾将领", 2), ("黄巾将领", 1), ("黄巾将领", 1)],   # D8：杂兵再 −1 级
    "b7": [("白马义从", 5), ("白马义从", 5), ("白马义从", 4), ("白马义从", 4)],   # 弓骑压骑 1.4，三骑队伍吃亏
    "b6": [("山贼头目", 6), ("山贼喽啰", 4), ("山贼喽啰", 4), ("山贼喽啰", 3), ("山贼喽啰", 3)],   # 奖励战：人数 ≤ 上阵×1.75＝5
    "b8": [("吕布", 9), ("并州骑", 4), ("并州骑", 4), ("并州骑", 4)],   # 守关与单挑脚本未建模，模拟为下限
    "b9": [("流寇头目", 9), ("流寇", 7), ("流寇", 7), ("流寇", 6), ("流寇", 6)],   # 主线 8 级、3 人（E1）
    "b10": [("文丑", 11), ("袁军骑", 9), ("袁军骑", 9), ("袁军骑", 8), ("袁军骑", 8)],
    "b11": [("麴义", 11), ("先登死士", 10), ("先登死士", 10), ("强弩手", 9), ("强弩手", 9)],
    "b13": [("曹军步卒", 11), ("曹军步卒", 10), ("曹军弓手", 10), ("曹军弓手", 10)],   # 枪弓阵克三骑，4 人
    "b12": [("管亥", 12), ("黄巾余部", 11), ("黄巾余部", 11), ("黄巾余部", 10), ("黄巾余部", 10), ("黄巾余部", 9)],
    "b14": [("曹仁", 14), ("曹军骑兵", 12), ("曹军骑兵", 12), ("曹军骑兵", 11), ("曹军骑兵", 11)],
    "b15": [("散兵", 13), ("散兵", 13), ("流寇", 12), ("流寇", 12), ("散兵", 12), ("散兵", 11)],
}
def battles_for(P):
    if P.get("growth", "v1") != "v2": return BATTLES
    return [(b[0], b[1], b[2], BATTLES_V2_OVERRIDES.get(b[0], b[3]), b[4], b[5]) for b in BATTLES]
