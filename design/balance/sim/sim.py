# -*- coding: utf-8 -*-
"""蒙特卡洛战斗模拟：半即时格子战棋（AT 行动条），第一篇章 15 场战斗。
无随机伤害（设计原则）；随机性来自布阵位置、行动条初值抖动、目标平手打破与玩家策略的小扰动。
用法：python3 sim.py [v1|v2] [runs]
"""
import sys, random, math, statistics
from dataclasses import dataclass, field
from typing import List, Optional
import model as M

@dataclass
class Unit:
    g: M.General; team: str; level: int; st: M.Stats
    hp: int; ep: int; cp: int; x: int; y: int; at: float
    move: int; rng: int; elem: str
    crafts: List[M.Skill]; strategies: list
    floor: float = 0.0
    troop: str = "inf"
    defending: bool = False; moved_this_turn: bool = False
    statuses: list = field(default_factory=list)   # (name, atkmul, defmul, guard, turns)
    pending: Optional[tuple] = None                # (skill, mul, target_unit or tile)
    name: str = ""
    def alive(self): return self.hp > 0
    def atk_mul(self):
        lo = min([s[1] for s in self.statuses if s[1] < 1] + [1.0]); hi = max([s[1] for s in self.statuses if s[1] > 1] + [1.0]); return lo * hi
    def def_mul(self):
        lo = min([s[2] for s in self.statuses if s[2] < 1] + [1.0]); hi = max([s[2] for s in self.statuses if s[2] > 1] + [1.0]); return lo * hi
    def guard(self): return max([s[3] for s in self.statuses] + [0.0])
    def add_status(self, name, atkm=1.0, defm=1.0, guard=0.0, turns=1):
        self.statuses = [s for s in self.statuses if s[0] != name]; self.statuses.append((name, atkm, defm, guard, turns))
    def tick(self):
        self.statuses = [(n, a, d, g, t - 1) for (n, a, d, g, t) in self.statuses if t - 1 > 0]

def dist(a, b): return abs(a.x - b.x) + abs(a.y - b.y)

class Battle:
    def __init__(self, P, party, enemies, W=14, H=12, seed=0, log=False):
        self.P = P; self.W = W; self.H = H; self.rnd = random.Random(seed); self.log = log
        self.units: List[Unit] = []; self.now = 0.0; self.events = []
        self.stats = {"dmg_by": {}, "skill_uses": {}, "skill_dmg": {}, "ep_spent": 0, "cp_spent": 0, "casts": 0, "crafts": 0}
        # 我方在南面 3 行，敌方在北面 3 行
        px = list(range(3, 11)); self.rnd.shuffle(px)
        for i, u in enumerate(party):
            u.x, u.y = px[i], self.rnd.choice([0, 1, 2]); u.at = self.rnd.uniform(0, 15); self.units.append(u)
        ex = list(range(2, 12)); self.rnd.shuffle(ex)
        for i, u in enumerate(enemies):
            u.x, u.y = ex[i], H - 1 - self.rnd.choice([0, 1, 2]); u.at = self.rnd.uniform(0, 15); self.units.append(u)
        self.resolve_overlaps()
    def resolve_overlaps(self):
        seen = set()
        for u in self.units:
            while (u.x, u.y) in seen:
                u.x = (u.x + 1) % self.W
            seen.add((u.x, u.y))
    def occupied(self, x, y, exclude=None):
        return any(v.alive() and v is not exclude and v.x == x and v.y == y for v in self.units)
    def round_no(self): return 1 + int(max(0, self.now) // 100)
    def foes(self, u): return [v for v in self.units if v.alive() and v.team != u.team]
    def friends(self, u): return [v for v in self.units if v.alive() and v.team == u.team and v is not u]
    def delay(self, u, cost): return M.at_delay(cost, u.st.speed, self.P)

    # 可达格：无地形，BFS，不能穿过敌人，可穿过友军但不能停
    def reachable(self, u):
        start = (u.x, u.y); best = {start: 0}; frontier = [start]
        while frontier:
            nxt = []
            for (x, y) in frontier:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    n = (x + dx, y + dy)
                    if not (0 <= n[0] < self.W and 0 <= n[1] < self.H): continue
                    blocker = next((v for v in self.units if v.alive() and v.x == n[0] and v.y == n[1]), None)
                    if blocker is not None and blocker.team != u.team: continue
                    d = best[(x, y)] + 1
                    if d > u.move or (n in best and best[n] <= d): continue
                    best[n] = d; nxt.append(n)
                    # ZOC：进入敌方相邻格停止
                    if any(v.alive() and v.team != u.team and abs(v.x - n[0]) + abs(v.y - n[1]) == 1 for v in self.units):
                        pass
            frontier = nxt
        return [p for p in best if not self.occupied(p[0], p[1], exclude=u)] + [start]

    def hit(self, a: Unit, t: Unit, dmg, skill_name="普攻", craft=False, interrupt=False, counter=False):
        if t.floor > 0:
            fl = math.ceil(t.st.hp * t.floor); dmg = max(0, min(dmg, t.hp - fl)) if t.hp > fl else 0
        t.hp = max(0, t.hp - dmg)
        if t.floor > 0 and t.hp <= math.ceil(t.st.hp * t.floor):
            t.hp = 0; t.floor = 0   # 阈值退走（首领退走即本场胜利，近似）
            for v in self.units:
                if v.team == t.team: v.hp = 0
        a.cp = min(100, a.cp + M.cp_on_hit(a.st, self.P, craft, a.troop)); t.cp = min(100, t.cp + M.cp_on_taken(self.P))
        # 枪兵反击：被骑系近战命中后反击一次普攻（T6，限定对骑系）
        if self.P.get("troop_v2") and t.alive() and t.troop == "spear" and M.BIG[a.troop] == "骑" and a.rng <= 1 and not counter:
            cd = self.dmg(t, a, 1.0, True, None)
            a.hp = max(0, a.hp - cd); self.stats["dmg_by"][t.name] = self.stats["dmg_by"].get(t.name, 0) + cd
            self.stats["counters"] = self.stats.get("counters", 0) + 1
        if interrupt and self.P.get("interrupt") and t.pending and t.alive():   # 咏唱打断：武技/绝学命中或水系推迟，退还一半策力（S4）
            ps = t.pending[0]; t.pending = None; t.ep = min(M.ep_max(t.st, self.P, t.troop), t.ep + ps.cost // 2)
            self.stats["interrupts"] = self.stats.get("interrupts", 0) + 1
        self.stats["dmg_by"][a.name] = self.stats["dmg_by"].get(a.name, 0) + dmg
        k = (a.name, skill_name); self.stats["skill_dmg"][k] = self.stats["skill_dmg"].get(k, 0) + dmg
        if not t.alive():
            a.cp = min(100, a.cp + M.cp_on_kill(self.P)); t.pending = None
            if self.log: print(f"  {t.name} 撤退")

    def dmg(self, a, t, coef, might, elem, moved=False, troop=True, melee=None):
        atk = a.st.atk if might else a.st.mat; dfn = t.st.dfn if might else t.st.mdef
        if melee is None: melee = (a.rng <= 1)
        adv = M.adv_mul(a.troop, t.troop, elem, t.elem, self.P, moved and might, troop, melee)
        taken = max(self.P["min_taken"], 1 - max(0.5 if t.defending else 0, t.guard()))
        return M.damage(atk, dfn, coef, adv, taken, 1.0, a.atk_mul(), t.def_mul(), self.P)

    def area(self, s: M.Skill, u: Unit, target):
        # target: Unit 或 (x,y)
        tx, ty = (target.x, target.y) if isinstance(target, Unit) else target
        if s.area == "single": return [(tx, ty)]
        if s.area == "circle": return [(tx + dx, ty + dy) for dx in range(-s.radius, s.radius + 1) for dy in range(-s.radius + abs(dx), s.radius - abs(dx) + 1)]
        if s.area == "selfcircle": return [(u.x + dx, u.y + dy) for dx in range(-s.radius, s.radius + 1) for dy in range(-s.radius + abs(dx), s.radius - abs(dx) + 1)]
        # line
        ddx, ddy = tx - u.x, ty - u.y
        if abs(ddx) >= abs(ddy): d = (1 if ddx > 0 else -1, 0)
        else: d = (0, 1 if ddy > 0 else -1)
        side = (-d[1], d[0]); out = []
        for k in range(1, s.length + 1):
            for w in range(-(s.width // 2), s.width // 2 + 1):
                out.append((u.x + d[0] * k + side[0] * w, u.y + d[1] * k + side[1] * w))
        return out

    def apply(self, u: Unit, s: M.Skill, target, mul=1.0, log_name=None, moved=False):
        tiles = set(self.area(s, u, target)); name = log_name or s.name
        self.stats["skill_uses"][(u.name, name)] = self.stats["skill_uses"].get((u.name, name), 0) + 1
        hostile = s.effect in ("damage", "debuff")
        victims = [v for v in self.units if v.alive() and (v.x, v.y) in tiles and ((v.team != u.team) if hostile else (v.team == u.team))]
        if s.area == "single" and isinstance(target, Unit) and target.alive() and target not in victims and (target.team != u.team) == hostile:
            victims = [target]
        for v in victims:
            if s.effect == "damage" or (s.effect == "debuff" and s.power > 0):
                self.hit(u, v, self.dmg(u, v, s.power * mul, s.might, s.elem, moved, troop=(s.kind == "craft")), name, craft=(s.kind == "craft"), interrupt=(s.kind == "craft" or s.at_delay > 0))
                if s.at_delay: v.at += s.at_delay * mul
            if s.effect == "debuff":
                if s.name == "破阵": v.add_status("破阵", defm=1 - s.amount, turns=s.turns)
                else: v.add_status("威慑", atkm=1 - s.amount, turns=s.turns)
            elif s.effect == "heal":
                h = min(v.st.hp - v.hp, int(v.st.hp * min(0.6, s.power * mul))); v.hp += h
                if s.amount: v.add_status(s.name, atkm=1 + s.amount, turns=s.turns)
                self.stats["skill_dmg"][(u.name, name)] = self.stats["skill_dmg"].get((u.name, name), 0) + h
            elif s.effect == "guard":
                v.add_status(s.name, guard=s.amount, turns=s.turns)
        if s.kind == "strategy":
            u.cp = min(100, u.cp + M.cp_on_cast(self.P))

    # ---------- 玩家策略（有经验的玩家的启发式） ----------
    def player_turn(self, u: Unit):
        foes = self.foes(u)
        if not foes: return
        reach = self.reachable(u)
        # 1. 治疗：有伤员 < 45%
        heals = [c for c in u.crafts if c.effect == "heal" and u.cp >= c.cost and c.area == "single"]
        if heals:
            hurt = [v for v in self.friends(u) + [u] if v.hp < 0.45 * v.st.hp]
            if hurt:
                v = min(hurt, key=lambda q: q.hp / q.st.hp)
                best = min(reach, key=lambda p: abs(p[0] - v.x) + abs(p[1] - v.y))
                if abs(best[0] - v.x) + abs(best[1] - v.y) <= heals[0].rng:
                    moved = best != (u.x, u.y); u.x, u.y = best; u.cp -= heals[0].cost
                    self.apply(u, heals[0], v); self.stats["crafts"] += 1
                    u.at = self.now + self.delay(u, M.AT_COST["craft"] + (M.AT_COST["move_extra"] if moved else 0)); return
        # 2. 策略（原地咏唱）：有伤害策略、够策力、范围内有目标，且伤/AT 不低于普攻
        strat = None
        for (s, li, over, mul) in sorted(u.strategies, key=lambda t: -t[0].power):
            if s.effect != "damage" or u.ep < s.cost: continue
            cand = [v for v in foes if dist(u, v) <= s.rng] if s.area != "line" else [v for v in foes if (v.x == u.x or v.y == u.y) and dist(u, v) <= s.length]
            if not cand: continue
            if s.area in ("circle",):
                # 选能覆盖最多敌人的中心
                best_t, best_n = None, 0
                for v in cand:
                    n = sum(1 for w in foes if (w.x, w.y) in set(self.area(s, u, v)))
                    if n > best_n: best_t, best_n = v, n
                if best_n >= 2 or len(foes) == 1: strat = (s, mul, best_t); break
            else:
                v = min(cand, key=lambda q: q.hp)
                # 单体策略：伤/AT 要 ≥ 普攻（粗估）
                d_s = self.dmg(u, v, s.power * mul, s.might, s.elem, troop=False) / (s.cast + s.recover)
                best_c = max([c.power for c in u.crafts if c.effect == "damage" and c.area == "single" and u.cp >= c.cost] + [1.0])
                d_a = self.dmg(u, v, best_c, True, None) / 110.0 if best_c > 1.0 else self.dmg(u, v, 1.0, True, None) / 100.0
                if d_s >= d_a * 1.0: strat = (s, mul, v); break
        if strat and not u.moved_this_turn:
            s, mul, v = strat; u.ep -= s.cost; self.stats["ep_spent"] += s.cost; self.stats["casts"] += 1
            u.pending = (s, mul, v); u.at = self.now + self.delay(u, max(10, s.cast - (10 if u.troop == "str" else 0))); return
        # 3. 走位＋攻击/武技
        # 目标：优先能击杀的最低兵力；否则最低兵力（首领权重）
        def tgt_score(v): return (0 if v.hp <= 0 else 1, v.hp)
        attackable = []
        for v in foes:
            for p in (reach if u.troop != "xbow" else [(u.x, u.y)]):
                if abs(p[0] - v.x) + abs(p[1] - v.y) <= u.rng: attackable.append((v, p)); break
        if attackable:
            # 自身范围武技：敌人≥2 在半径内
            for c in u.crafts:
                if c.effect == "damage" and c.area == "selfcircle" and u.cp >= c.cost:
                    best_p, best_n = None, 0
                    for p in reach:
                        n = sum(1 for v in foes if abs(p[0] - v.x) + abs(p[1] - v.y) <= c.radius)
                        if n > best_n: best_p, best_n = p, n
                    if best_n >= 2:
                        moved = best_p != (u.x, u.y); u.x, u.y = best_p; u.cp -= c.cost; self.stats["cp_spent"] += c.cost; self.stats["crafts"] += 1
                        self.apply(u, c, (u.x, u.y))
                        u.at = self.now + self.delay(u, M.AT_COST["craft"] + (M.AT_COST["move_extra"] if moved else 0)); return
            v, p = min(attackable, key=lambda t: tgt_score(t[0]))
            if self.rnd.random() < 0.1: v, p = self.rnd.choice(attackable)   # 玩家的小失误
            moved = p != (u.x, u.y); u.x, u.y = p
            single = [c for c in u.crafts if c.effect in ("damage", "debuff") and c.area == "single" and u.cp >= c.cost and (c.rng < 0 and dist(u, v) <= 1 or c.rng >= dist(u, v))]
            single = [c for c in single if c.power > 0]
            if single:
                c = max(single, key=lambda q: q.power)
                u.cp -= c.cost; self.stats["cp_spent"] += c.cost; self.stats["crafts"] += 1
                self.apply(u, c, v, moved=moved)
                u.at = self.now + self.delay(u, M.AT_COST["craft"] + (M.AT_COST["move_extra"] if moved else 0)); return
            self.hit(u, v, self.dmg(u, v, 1.0, True, None, moved), "普攻")
            self.stats["skill_uses"][(u.name, "普攻")] = self.stats["skill_uses"].get((u.name, "普攻"), 0) + 1
            u.at = self.now + self.delay(u, M.AT_COST["attack"] + (M.AT_COST["move_extra"] if moved else 0)); return
        # 4. 无法攻击：向最近敌人靠近（弓/军师保持距离 2）
        v = min(foes, key=lambda q: dist(u, q))
        keep = 2 if u.rng >= 3 else 0
        best = min(reach, key=lambda p: abs(abs(p[0] - v.x) + abs(p[1] - v.y) - keep))
        u.x, u.y = best
        u.defending = True
        u.at = self.now + self.delay(u, M.AT_COST["move"] + M.AT_COST["guard"])

    # ---------- 敌方 AI（原型固定规则：军师放策略；其余冲向最近者普攻；具名将领隔次用绝学） ----------
    def enemy_turn(self, u: Unit, turn_count):
        foes = self.foes(u)
        if not foes: return
        reach = self.reachable(u)
        for (s, li, over, mul) in u.strategies:
            if s.effect == "damage" and u.ep >= s.cost:
                cand = [v for v in foes if dist(u, v) <= s.rng]
                if cand:
                    v = min(cand, key=lambda q: q.hp); u.ep -= s.cost; u.pending = (s, mul, v); u.at = self.now + self.delay(u, max(10, s.cast - (10 if u.troop == "str" else 0))); return
        attackable = []
        for v in foes:
            for p in (reach if u.troop != "xbow" else [(u.x, u.y)]):
                if abs(p[0] - v.x) + abs(p[1] - v.y) <= u.rng: attackable.append((v, p)); break
        if attackable:
            v, p = min(attackable, key=lambda t: (t[0].hp, self.rnd.random()))
            moved = p != (u.x, u.y); u.x, u.y = p
            crafts = [c for c in u.crafts if c.kind == "craft" and c.effect == "damage" and u.cp >= c.cost and (c.rng < 0 and dist(u, v) <= 1 or c.rng >= 0)]
            if crafts and turn_count % 2 == 0:
                c = crafts[0]; u.cp -= c.cost; self.apply(u, c, v)
                u.at = self.now + self.delay(u, M.AT_COST["craft"] + (M.AT_COST["move_extra"] if moved else 0)); return
            self.hit(u, v, self.dmg(u, v, 1.0, True, None, moved), "普攻")
            u.at = self.now + self.delay(u, M.AT_COST["attack"] + (M.AT_COST["move_extra"] if moved else 0)); return
        v = min(foes, key=lambda q: dist(u, q))
        best = min(reach, key=lambda p: abs(p[0] - v.x) + abs(p[1] - v.y)); u.x, u.y = best
        u.at = self.now + self.delay(u, M.AT_COST["move"] + M.AT_COST["wait"])

    def run(self, max_rounds=20, events=None, win_round=None):
        turns = {}
        while True:
            alive = [v for v in self.units if v.alive()]
            if win_round and self.round_no() >= win_round and any(v.team == "P" for v in alive): return self.result(True)
            if not any(v.team == "P" for v in alive): return self.result(False)
            if not any(v.team == "E" for v in alive): return self.result(True)
            if self.round_no() > max_rounds: return self.result(False, timeout=True)
            u = min(alive, key=lambda v: (v.at, 0 if v.team == "P" else 1, self.rnd.random()))
            self.now = u.at
            if events: events(self, u)
            u.defending = False; u.moved_this_turn = False
            turns[u.name] = turns.get(u.name, 0) + 1
            u.ep = min(M.ep_max(u.st, self.P, u.troop), u.ep + M.ep_regen(u.st, self.P))
            if u.pending:
                s, mul, tgt = u.pending; u.pending = None
                self.apply(u, s, tgt, mul); u.at = self.now + self.delay(u, s.recover); u.tick(); continue
            if u.team == "P" and not getattr(u, "ally_ai", False): self.player_turn(u)
            elif u.team == "P": self.enemy_turn(u, turns[u.name])
            else: self.enemy_turn(u, turns[u.name])
            u.tick()

    def result(self, win, timeout=False):
        party = [v for v in self.units if v.team == "P" and not getattr(v, "ally_ai", False)]
        return dict(win=win, timeout=timeout, rounds=self.round_no(),
                    party_hp=sum(max(0, v.hp) for v in party) / sum(v.st.hp for v in party),
                    party_down=sum(1 for v in party if not v.alive()), stats=self.stats)

# ---------- 建立单位 ----------
def make_unit(name, team, level, P, lib=None, slots=None, intel_base_for_cord=None):
    g = M.GENERALS[name]; st = M.compute(g, level, P)
    crafts_list = []
    for c in g.crafts:
        sk = M.CRAFTS[c]
        if sk.name == "横扫" and P.get("sweep_power"):
            import dataclasses; sk = dataclasses.replace(sk, power=P["sweep_power"])
        crafts_list.append(sk)
    troop = M.troop_of(g, P)
    u = Unit(g, team, level, st, st.hp, M.ep_max(st, P, troop), M.cp_start(P), 0, 0, 0.0,
             move=(M.TROOP_MOVE[troop] if P.get("troop_v2") else (5 if troop == "cav" else 2 if troop == "str" else 3)),
             rng=(M.TROOP_RANGE[troop] if P.get("troop_v2") else (3 if troop == "arc" else 2 if troop == "str" else 1)),
             elem=(P.get("zhangjiao_elem", "土") if name == "张角" else M.unit_element(g, P)), crafts=[sk for sk in crafts_list if sk.kind == "craft"],
             strategies=[(sk, 0, 0, 1.0) for sk in crafts_list if sk.kind == "strategy"],
             floor=g.floor, name=name)
    u.troop = troop
    if team == "P" and lib is not None:
        c = M.CIRCUITS[g.school]; slots = slots or M.default_slots(name, lib, P)
        cord_int = st.intellect if P["cord_by"] == "level_int" else g.intellect
        u.strategies += M.lit_strategies(c, slots, cord_int, P)
        u.crafts += [t[0] for t in M.lit_techniques(c, slots, cord_int, P)]
    return u

def party_for(bid, exp, P, lib):
    lv = M.level_for_exp(exp)
    names = ["刘备", "关羽", "张飞"]
    if bid in M.PARTY4: names.append("赵云")   # 0.4 磐河起客将 / 0.5 后备（模拟按 4 人上阵近似）；b9 三人（E1）
    return [make_unit(n, "P", lv, P, lib) for n in names], lv

def simulate_battle(b, P, runs=200, seed=1, overrides=None):
    bid, bname, exp, enemies, bexp, note = b
    lib = M.library(); res = []
    for r in range(runs):
        party, lv = party_for(bid, exp, P, lib)
        if overrides: overrides(party)
        ens = [make_unit(n, "E", l, P) for n, l in enemies]
        if bid == "b5":
            ens[0].floor = 0.3   # 张角兵力低于 30% 即退走（本场胜利）
        if bid == "b4":
            def ev(bt, u):   # 第 3 回合火起：敌军损 10%，混乱（物攻 −30%，物防 −20%）4 次行动近似为 2 回合
                if not getattr(bt, "_fired", False) and bt.round_no() >= 3:
                    bt._fired = True
                    for v in bt.units:
                        if v.team == "E" and v.alive(): v.hp = max(1, v.hp - int(v.st.hp * 0.1)); v.add_status("混乱", atkm=0.7, defm=0.8, turns=2)
                if not getattr(bt, "_cao", False) and bt.round_no() >= 6:   # 第 6 回合曹操与两队官军骑兵从东侧加入（友军，电脑操作）
                    bt._cao = True
                    cao = M.General("cao_cao", "曹操", "cav", 78, 96, 94, school="兵", crafts=["突击"]); rider = M.General("han_cav", "官军骑兵", "cav", 58, 52, 30, school="兵")
                    for g, lv, y in ((cao, 6, 6), (rider, 4, 5), (rider, 4, 7)):
                        st = M.compute(g, lv, P)
                        nu = Unit(g, "P", lv, st, st.hp, 0, 0, bt.W - 1, y, bt.now + 1, 5, 1, "火", [M.CRAFTS[c] for c in g.crafts], [], 0, name=g.name + str(y))
                        nu.ally_ai = True; nu.troop = "cav"; bt.units.append(nu)
            out = Battle(P, party, ens, seed=seed + r).run(events=ev)
        else:
            out = Battle(P, party, ens, seed=seed + r).run(win_round=6 if bid == "b5" else None)
        res.append(out)
    return dict(id=bid, name=bname, level=lv, enemies=enemies,
                win=sum(1 for o in res if o["win"]) / runs,
                rounds=statistics.mean(o["rounds"] for o in res),
                hp=statistics.mean(o["party_hp"] for o in res),
                down=statistics.mean(o["party_down"] for o in res),
                casts=statistics.mean(o["stats"]["casts"] for o in res),
                interrupts=statistics.mean(o["stats"].get("interrupts", 0) for o in res),
                share={n: statistics.mean(o["stats"]["dmg_by"].get(n, 0) for o in res) for n in ["刘备", "关羽", "张飞", "赵云"]},
                crafts=statistics.mean(o["stats"]["crafts"] for o in res),
                res=res)

if __name__ == "__main__":
    ver = sys.argv[1] if len(sys.argv) > 1 else "v1"; runs = int(sys.argv[2]) if len(sys.argv) > 2 else 100
    P = M.PARAMS[ver]
    print(f"参数集 {ver}，每场 {runs} 次")
    print("| 战斗 | 我方等级 | 胜率 | 平均回合 | 结束时我方兵力 | 平均倒下人数 | 策略次数 | 武技次数 | 咏唱被打断 | 伤害占比 |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    for b in M.battles_for(P):
        if b[5] == "回合制": continue
        o = simulate_battle(b, P, runs)
        sh = o["share"]; tot = sum(sh.values()) or 1
        print(f"| {o['id']} {o['name']} | {o['level']} | {o['win']:.0%} | {o['rounds']:.1f} | {o['hp']:.0%} | {o['down']:.2f} | {o['casts']:.1f} | {o['crafts']:.1f} | {o['interrupts']:.1f} | 刘{sh['刘备']/tot:.0%} 关{sh['关羽']/tot:.0%} 张{sh['张飞']/tot:.0%} 赵{sh['赵云']/tot:.0%} |")
