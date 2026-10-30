# AI 武将命运系统 — 可实装规格 v5.1

> **状态**：规格稿 v5.1（2026-10-28 用户“继续”后进入实装；Q1 已定，Q2–Q4 按推荐执行）。v5.0 于 2026-10-27 经 CCGS 三方评审（技术总监 CONCERNS、叙事总监 CONCERNS、制作人 OPTIMISTIC）后修订为 v5.1，评审意见与处理见第 13 节。取代 v4 建议稿（`AI_CHARACTER_FATE_DESIGN.md`）的第 3–9、12–15 节；v4 保留作为设计背景与决策记录。
> **日期**：2026-10-27
> **目标读者**：实装线程（`/create-stories`、`/dev-story`）、内容管线工具的编写者、叙事与数据策划。
> **已定前提（不再讨论）**：
> 1. 剧情按史实走，重大事件（战役胜负、城池与政权归属、称帝）不能改；只能通过设计好的“救人条件”让本该死去的人物不死（用户 2026-10-22 19:36）。
> 2. 游戏运行时**完全离线**；大模型只在开发期用来批量生成文本并经人工审校（用户 2026-10-22）。
> 3. 年份只在主线节点跳（传统 JRPG）。三线分卷＋赤壁共同终章＋史实结局加尾声变体（用户 2026-10-22 19:44）。
> 4. 与兵书系统（`design/bingshu-system.md` §3.7、§3.11）字段对齐。
> **配套文件**：`docs/architecture/adr-0013-fate-engine.md`（命运引擎架构决定；原稿编号 0012，合入仓库时因编号已占用顺延为 0013）。
> **术语**：本文把 v4 的“AI 裁决”一律改称“结算”（settle）；运行时没有 AI，只有确定性规则；“AI”一词只用于开发期内容管线。

---

## 0. 一页概览

| 问题 | v5.1 的回答 |
| --- | --- |
| 什么会变 | 人物层：记忆、对主角的态度（好感／信任／六看法）、人际关系、在野／出仕／阵营、所在城市、是否可招募、是否在队、持有物；外加“救人条件”解除 `dies` 锚点 |
| 什么不变 | 主线节点、战役结果、城池政权归属、HARD 锚点、年份、同死者 |
| 什么时候变 | 只在**进入下一个主线节点**时结算一次；阶段内只有即时态度变化和脚本化加入／离队 |
| 谁来决定 | 确定性规则引擎：候选过滤 → 打分 → 定点 softmax 抽签 → 全序仲裁；**偏离默认路线必须有“因”**，而且因与去向同向 |
| 文本从哪来 | 开发期用大模型按（人物 × 候选 × 阶段组 × 好感档 × 槽位）批量生成，人工全审后作为文本库随游戏发布；运行时查表，查不到退到模板 |
| 存档 | 整份存档一次原子提交（含命运段、库存、队伍）；读档不重抽；跨篇章用 `ChronicleRecord` 按字段分主组装 |
| 分期 | P0a 引擎核心（与 0.3 并行，云端线程）→ P0b 游戏内集成（0.4 第一个故事）→ P0.5 第一篇章命运数据 → P1 文本库（先做 100 条校准批） |

---

## 1. v4 审读：矛盾与漏洞清单

按照线程要求，先列出 v4 中会阻碍实装的问题，再在后文逐条解决。编号 G-xx 在后文引用。技术总监评审认为其中 G-04、G-06、G-12、G-18、G-21、G-22、G-23、G-26 在 v5.0 尚未真正闭合，v5.1 已按第 13 节的意见补齐。

| # | 问题 | 后果 | v5.1 处理 |
| --- | --- | --- | --- |
| G-01 | v4 第 6.2 步⑤、6.3、7、11.3、12（AI 缓存）、13.1（`LlmFateResolver`）、14.2（`aiMargin`）、15（断网）仍按“运行时调模型”设计，与决定 2 矛盾 | 实装者不知道该不该做 AI 网关 | 运行时流程改为全确定性（§5）；在线模式要点整体降级为附录 B |
| G-02 | 救人条件“当场判定、下一次重算并入”，但重算发生在“进入节点”时，先后顺序没有定义 | `dies` 锚点在哪一步生效说不清 | §3 时间模型：`dies` 锚点定义为“阶段末”，在下一节点的结算第①步并入 |
| G-03 | 阶段 id 按年份命名，而第一篇章剧本用 N1–N8（N4 一年含两节，N2→N3 跳 4 年） | 数据对不上剧本 | id 规范 `<篇章>.<节点>`，年份是节点属性（§3.2） |
| G-04 | v4 `MemoryEntry` 与 0.2 已实装的记忆最小版不兼容 | 0.2 存档无法迁移，Yarn 条件要重写 | v5 `MemoryEntry` 是 0.2 的超集，保留 key 覆盖语义与查询函数；迁移表给 `seen` 类 importance 2（§4.6、§7.1） |
| G-05 | `playerImpression` 以“player”为主体，但三线主角不同、终章切换主角 | 跨篇章时“玩家”没有主体 | 态度一律以**主角人物 id** 为键（§4.6） |
| G-06 | T2 的史实惯性低于性格／抱负差值，无玩家交互也会偏离 | 玩家没做任何事人物也乱跑 | “**偏离需因**”：非默认候选必须匹配带标签的因果令牌（§5.2 ③） |
| G-07 | “玩家影响”项靠推断；例子把“随公孙瓒”算作远离玩家 | 公式不可实装 | 每个候选显式写 `playerProximity`（§4.3） |
| G-08 | T0“城市、阵营不可变”，但被救者进入存活路线必然改变 | 校验器误判 | 被救者转入 `T1S`，只受 `survivorConstraints` 约束（§4.5） |
| G-09 | 救人对象身居权位（孙策）不死就改变政权归属 | 东吴线孙策救人无法成立 | 救人案例必填 `powerTransfer`（§4.5） |
| G-10 | 周瑜 210 年卒，东吴线建议止于 200 年 | 六人之一没有落点 | 待决问题 Q1（§12） |
| G-11 | 救人条件含 `battleObjective`，场景加载前必须知道是否生成该目标 | 战斗配置不知道加载哪个变体 | `eligibility` ＋ `fulfillment` 两段（§4.5、§5.4） |
| G-12 | “离队前预警对白”没有机制 | 玩家来不及挽回 | 两步提交 `pendingTransitions`，提交时复核阈值而非重抽（§5.2 ⑤） |
| G-13 | “每个可招募人物至少一条招募路径”与敌方 HARD 锚点不可同时成立 | 可达性证明永远失败 | `recruitableIn` 按篇章；锚点与在队冲突者必须有 `departureSceneId`（§4.3、§8.2） |
| G-14 | 仲裁要用官职／驻军／阵营底线，但没有数据 | 第⑥步无法实装 | `WorldSlots`（§4.7） |
| G-15 | 知识隔离没有规则 | 分发逻辑无法实装 | HISTORY 事件带 `scope` 与 `delayStages`（§5.5） |
| G-16 | 6.3、11.3 针对不做的模式 | 误导排期 | 删除；开发期成本改按文本库规模估算（§6.6） |
| G-17 | 存档“AI 输出缓存”“待提交日志”针对在线模式 | 多做无用功 | 保留原子提交协议并改为整档事务（§7.2） |
| G-18 | 跨篇章元存档只写“以后另立 ADR” | 共同终章无法读取三线结果 | `ChronicleRecord` 按字段分主组装（§4.9、§7.3） |
| G-19 | 验收标准编号错；AC2 是调参目标；AC6 针对运行时 AI | QA 无法判定 | 重写并按分期归属（§8.4） |
| G-20 | 装备、宝物归属没有数据表示 | 退回逻辑无法区分 | 持有物带 `origin`（§4.6、§4.10） |
| G-21 | 反应矩阵只给了例子 | 无法填初值 | 完整初值表，Q16.16 全程（§5.1） |
| G-22 | T3 可死，但既无“因”也无预警 | 支线人物无故死亡 | T3 死亡候选需 RANDOM／HISTORY 因并走两步提交；剧情也可直接写入（§5.2） |
| G-23 | 定点 softmax 未定义 exp 实现 | 不同实现结果不同 | Q16.16、`long` 中间值、随包 EXP 表、取整规则（§5.3） |
| G-24 | 没有校验“每个（人物，阶段）都有且只有一条默认” | 运行时候选为空 | V-03：默认候选无前置且满足锚点（§8.1） |
| G-25 | 第一篇章不设救人条件，P0 无法验证救人流程 | 到第五幕才第一次跑 | P0 加一个仅测试数据的救人案例（§10） |
| G-26 | 12A 未指定哈希与随机流 | 种子派生不可复现 | 随机流表、PCG/SplitMix/FNV 参数（§5.3） |

---

## 2. 范围与原则

1. **人物层可变、事件层不变**。引擎只读写 `CharacterState`、`WorldFateState`；它没有任何接口能改剧情标志（ADR-0006）之外的世界结果。
2. **只选不生成**。运行时所有去向都来自策划预写的候选库；所有文本都来自审校过的文本库或模板。
3. **偏离需因、因果同向**。人物离开默认路线，必须能指出一个具体原因（玩家的某次交往、某个预设随机事件、某件史事、与某人的情谊），且候选声明它需要哪一类原因。原因会显示在人物志里。
4. **同输入同输出**。结算是纯函数，可在 EditMode 测试里跑一整篇章。
5. **模式 0 是完整游戏**。文本库缺条目时退到模板。
6. **数据先于代码**。候选、锚点、救人条件、文本库都是数据文件，由 CI 校验；校验失败则构建失败。

不在本文范围：运行时在线模式（附录 B）、玩家自由打字对话、数值成长曲线。

---

## 3. 时间模型

### 3.1 篇章、节点、阶段

- **篇章**（volume）：一条主角线的一段，如第一篇章「崛起」（`lb1`）。每个篇章有 `protagonistId`（刘备线为 `liu_bei`）。
- **主线节点**（node）：年份跳动的点。第一篇章为 N1–N8 加尾声（`CHAPTER1_RISE_SCRIPT.md` §3）。
- **阶段**（stage）：从进入节点 N 起、到进入节点 N+1 止的全部游玩。阶段 id ＝ 起始节点 id。

### 3.2 id 规范

| 对象 | 格式 | 例 |
| --- | --- | --- |
| 篇章 | `<线><序号>` | `lb1`（刘备线第一篇章）、`cc1`、`sc1` |
| 节点 | `<篇章>.N<序号>`；尾声为 `.E` | `lb1.N4`、`lb1.E` |
| 阶段 | 同起始节点 id | `lb1.N4` |
| 人物 | 小写拼音下划线 | `zhao_yun` |
| 候选 | `<人物>.<篇章>.<短语>` | `zhao_yun.lb2.wander_changshan` |
| 救人案例 | `rescue.<人物>.<节点>` | `rescue.guan_yu.lb5.N1` |

节点数据表 `nodes.json`：`{ "id":"lb1.N4", "volume":"lb1", "year":190, "eraName":"初平元年", "title":"投奔公孙瓒／十八路诸侯", "next":"lb1.N5" }`。年份是节点属性，**不是** id 的一部分（G-03）。**刘备线篇章划分（2026-10-27 剧本线程已定稿至第四篇章）**：lb1「崛起」184–196、lb2「浮沉」196–207、lb3「赤壁」208–211、lb4「入蜀」211–219、lb5「终幕」219–223（第五篇章未写，边界待定）。据此首批救人案例落点：陈宫 lb2（199 白门楼，第二篇章剧本 §10）、糜夫人 lb3（208 长坂，第三篇章 §10.1）、周瑜 lb3（209 南郡中箭，逸话，用户 2026-10-27 决定）、庞统 lb4（214 雒城，第四篇章 §10.1）、关羽 lb5（219）。曹操线 `cc3`、东吴线 `sc1` 仍为占位编号，`canonLevel=Provisional`（叙事总监意见 11）。

### 3.3 一个节点内的事件顺序（G-02）

```
进入节点 N
  ├─ ⓪ 预写：原子写一次“日志冻结 + 目标节点 id + pendingRecompute=true”（§7.2）
  ├─ ① 结算（§5.2）：读上一阶段事件日志 → 写记忆 → 并入救人结果 → 过滤/打分/抽签/仲裁 → 整档原子提交
  ├─ ② 节点剧情：脚本化过场与主线战斗（含救人判定 §5.4）；脚本化的加入/离队在这里发生
  └─ ③ 自由阶段：探索、支线、招募、赠礼……只追加事件日志，不改人物去向
进入节点 N+1 → 回到 ⓪
```

规定：
- `dies` 锚点的 `stage` 写**死亡发生的节点**；它在 N+1 的结算第①步并入（`alive=false`，发 HISTORY 记忆）。从节点 N 的剧情播完到进入 N+1 之间，该人物 `pendingDeath=true`：场景中不再出现（节点剧情负责移除），条件谓词 `alive(id)` 返回 **false**（定义为 `alive && !pendingDeath`），`fate_location` 返回空（技术总监 F-08）。
- 救人成功在节点剧情里当场写 `rescueResults[caseId]=success`，在 N+1 的第①步并入。
- 脚本化加入／离队（如赵云 N6 入队、N8 末辞别）是**日志事件**（`type: recruit`，带 `scripted:true`）；Gameplay 立即投影到 `recruitState` 供显示与 Yarn 查询，结算时由日志回放得到同一状态（单向数据流，F-13）。它们**不走候选打分**。
- 阶段内对“在场人物”的定义（偏离预算用）：`alive && !pendingDeath && location ∈ 本篇章开放区域`。

---

## 4. 数据结构

所有命运数据为 JSON 文本资产（理由见 ADR-0013），UTF-8，键名小驼峰。**所有参与打分、态度计算的数值在 JSON 中以整数存储**：权重与系数为 Q16.16（1.0＝65536），抱负权重为千分数（技术总监 F-11）；加载层不得经 `double` 转换。字段后标 `*` 为必填。

### 4.1 枚举

| 枚举 | 取值 |
| --- | --- |
| `Tier` | `T0` 锁定 · `T1` 锚点 · `T2` 宽松 · `T3` 原创 · `T1S` 存活者（仅由救人成功产生） |
| `MemoryKind` | `PLAYER` · `WITNESS` · `HISTORY` · `RANDOM` · `RUMOR` |
| `RecruitState` | `locked` · `unknown` · `known` · `available` · `inParty` · `reserve` · `departed` |
| `AnchorStrength` | `HARD` · `SOFT` |
| `CanonLevel` | `Established` · `Provisional` · `UnderReview` |
| `TextSlot` | `dongxiang` · `rumor` · `monologue` · `reunion` · `summary` · `hint` |
| `FavorBand` | `close`（好感 ≥ 40）· `neutral`（−20 ～ 39）· `distant`（< −20）|
| `ViewDim` | `benevolence` 仁德 · `valor` 勇武 · `wisdom` 智略 · `faith` 信义 · `authority` 威严 · `deceit` 诡诈 |
| `InteractionTag` | `benevolence` · `valor` · `wisdom` · `keep_promise` · `break_promise` · `authority` · `deceit` · `courtesy` · `insult` · `gift` · `gift_book` · `gift_treasure` · `fought_alongside` · `fought_against` · `spared` · `captured` |
| `CauseToken` | `PLAYER:<tag>`（如 `PLAYER:benevolence`）· `PLAYER:+`／`PLAYER:-`（任意正／负向交往）· `RANDOM:<eventId>` · `HISTORY:<eventId>` · `RELATION:<charId>` · `DEFAULT` |
| `ItemOrigin` | `own` · `player_given` |

### 4.2 `CharacterProfile`（档案层，只读）

```json
{
  "id": "zhao_yun", "name": "赵云", "courtesyName": "子龙",
  "tier": "T1",
  "ownerLine": "lb",
  "recruitableIn": ["lb1", "lb2", "lb3"],
  "factionsEligible": ["gongsun_zan", "liu_bei", "wanderer"],
  "biography": "常山真定人……（≤300 字，标注来源）",
  "personality": { "loyalty": 85, "righteousness": 90, "benevolence": 80, "courage": 85,
                   "cunning": 45, "fame": 30, "profit": 15, "pride": 35 },
  "ambitions": [ { "id": "find_true_lord", "weight": 700 }, { "id": "protect_people", "weight": 300 } ],
  "values": { "likes": ["benevolence", "keep_promise"], "dislikes": ["deceit", "insult"] },
  "voice": { "register": "沉稳简练", "selfRef": "云", "sampleLines": ["……"], "taboo": ["轻浮调笑"] },
  "growthCurveId": "general_spear_A",
  "wuxingAptitude": { "metal": "A", "wood": "A", "water": "A", "fire": "C", "earth": "S" },
  "favoredBooks": ["book_xxx"],
  "ownedItems": [ { "itemId": "spear_longdan", "origin": "own" } ],
  "powerPositions": [],
  "sources": ["【史实·裴注】《赵云传》注引《云别传》", "【演义】第 7 回"]
}
```

| 字段 | 说明 |
| --- | --- |
| `ownerLine`* | 跨篇章组装时，此人**去向类字段**以哪条线的记录为准（§7.3） |
| `recruitableIn`* | 可被招募的篇章列表；空数组＝只作人物卡（G-13） |
| `personality`* | 8 维 0–100：忠 loyalty、义 righteousness、仁 benevolence、勇 courage、谋 cunning、名 fame、利 profit、傲 pride |
| `ambitions`* | 1–3 条，权重千分数，和为 1000 |
| `wuxingAptitude`* / `favoredBooks` | 与兵书 §3.7、§3.11 共用：恰好 1 个 S、至少 1 个 C；赵云按兵书表为土 S、火 C |
| `powerPositions` | 史实权位 id（如孙策 `lord_jiangdong`）；非空者若有救人案例，必须有 `powerTransfer` |

### 4.3 `FateRoute`（命运层，只读）

示例按第一篇章剧本（赵云：N6 磐河入队为客将、随刘备行动到 N8 末辞别）修正（叙事总监 A-1、A-3、A-6）：

```json
{
  "characterId": "zhao_yun",
  "volume": "lb1",
  "activeFrom": "lb1.N5",
  "defaultRoute": [
    { "stage": "lb1.N5", "optionId": "zhao_yun.lb1.serve_gongsun" },
    { "stage": "lb1.N6", "optionId": "zhao_yun.lb1.follow_liubei_nominal" },
    { "stage": "lb1.N7", "optionId": "zhao_yun.lb1.follow_liubei_nominal" },
    { "stage": "lb1.N8", "optionId": "zhao_yun.lb1.follow_liubei_nominal" },
    { "stage": "lb1.E",  "optionId": "zhao_yun.lb1.return_gongsun" }
  ],
  "anchors": [
    { "id": "zhao_yun.anchor.lb1.N6.panhe", "stage": "lb1.N6", "strength": "HARD",
      "state": { "all": [ { "faction": ["zhao_yun", "gongsun_zan"] }, { "presentAt": ["zhao_yun", "battle_10_panhe"] } ] },
      "source": "【演义】第 7 回磐河；【史实·裴注】《云别传》投公孙瓒", "canonLevel": "Established" },
    { "id": "zhao_yun.anchor.lb1.N7.duchang", "stage": "lb1.N7", "strength": "HARD",
      "state": { "presentAt": ["zhao_yun", "battle_12_duchang"] }, "source": "剧本 §4 第八节（赵云参与北海为【原创改编】）", "canonLevel": "Provisional" },
    { "id": "zhao_yun.anchor.lb1.N8.xiapi", "stage": "lb1.N8", "strength": "HARD",
      "state": { "all": [ { "presentAt": ["zhao_yun", "battle_13_xiapi_east"] }, { "presentAt": ["zhao_yun", "battle_14_xiapi_defense"] } ] },
      "source": "剧本 §5 战斗 13、14", "canonLevel": "Provisional" }
  ],
  "fateOptions": [
    { "id": "zhao_yun.lb1.follow_liubei_nominal", "stages": ["lb1.N6", "lb1.N7", "lb1.N8"],
      "effect": { "faction": "gongsun_zan", "location": null, "recruitState": null, "role": "刘备主骑（名义上公孙瓒部将）", "relationTags": { "liu_bei": ["主骑"] } },
      "playerProximity": 1, "requiresCause": [], "twoStep": false,
      "summaryTemplate": "{p:zhao_yun}随{p:liu_bei}行动，名义上仍是公孙瓒部将。",
      "canonLevel": "Established", "source": "【史实·裴注】《云别传》“为先主主骑”" }
  ],
  "initialRelations": [ { "target": "gongsun_zan", "favor": 10, "trust": 40, "tags": [] } ],
  "randomEvents": []
}
```

原创去向（如 `zhao_yun.lb2.wander_changshan`：`stages ["lb2.N1"]`、`requiresCause ["PLAYER:benevolence", "PLAYER:keep_promise"]`、`playerProximity 1`、`effect { faction: wanderer, location: zhending, recruitState: available }`）放在第二篇章起始阶段，不放在第一篇章。

| 字段 | 说明 |
| --- | --- |
| `activeFrom` | 此前人物不在世界里、不结算（V-03 的范围从这里开始） |
| `defaultRoute`* | `activeFrom` 起每个阶段恰好一条；默认候选**不得有** `preconditions`（V-03） |
| `anchors[].state`* | 结构化条件（§4.4）。HARD：候选生成时过滤，提交前再校验；SOFT：允许偏离，偏离时必须有 `compensationSceneId` |
| `anchors[].departureSceneId` | 锚点与 `inParty` 冲突时必填；为空则该篇章不可招募（G-13） |
| `fateOptions[].effect` | `null` 字段表示“不改”；`recruitState` 不能写 `inParty`；`relationTags` 由候选写入关系标签（叙事总监 A-6） |
| `playerProximity`* | −1 远离主角／0 无关／+1 靠近主角 |
| `requiresCause`* | 允许本候选的因果令牌（§4.1 `CauseToken`）。**因与去向同向**：`playerProximity=+1` 的候选只能声明正向令牌（`PLAYER:<正向 tag>`、`PLAYER:+`、`RELATION:<主角阵营人物>`），`−1` 的只能声明负向令牌（`PLAYER:break_promise`、`PLAYER:deceit`、`PLAYER:insult`、`PLAYER:-`）；V-05 校验（技术总监 F-03c）。空数组只允许出现在默认候选 |
| `twoStep` | 离队、背叛、T3 `dies` 类候选必须为 true；其 `stages` 必须包含宣告阶段的**紧接下一阶段**（F-04） |
| `effect.aptitudeChange` / `itemTransfer` | 兵书 §3.7 预设数值；宝物随人流转（P0 只保留字段与引用校验，逻辑待兵书沙盒后，制作人意见 11） |

**在队人物的候选范围**（叙事总监 A-1）：`recruitState == inParty` 且由剧情写入（日志事件 `scripted:true`）的人物，本阶段只允许默认候选与 `twoStep` 离队候选进入打分；任何与该阶段 `presentAt` 锚点冲突的候选不得出现（V-04）。

通用候选库（T2 强制使用）：`generic.stay`、`generic.serve_nearest_lord`、`generic.retire`、`generic.wander`、`generic.join_protagonist`。

### 4.4 条件语言

只允许以下谓词，不允许算术与字符串表达式；构建期校验谓词名、参数类型与引用 id（IL2CPP 下用 `switch` 分派，不用反射）。

| 谓词 | 参数 | 说明 |
| --- | --- | --- |
| `all` / `any` / `not` | 条件数组／单条件 | 逻辑组合 |
| `nodeReached` | nodeId | 当前或更早的节点 |
| `flag` / `flagValue` | name ／ name, value | 剧情标志（ADR-0006） |
| `memory` | who, key[, value] | 0.2 的 `memory(who,key)==value` |
| `favorAtLeast` / `trustAtLeast` | who, value | 对主角 |
| `viewAtLeast` | who, dim, value | 对主角的某看法 |
| `relationAtLeast` | a, b, value | 两人之间好感 |
| `alive` | id | **`alive && !pendingDeath`**（F-08） |
| `faction` / `location` / `recruitState` | id, value | 人物状态 |
| `presentAt` | id, sceneId | 仅锚点用 |
| `factionDestroyed` | factionId | 世界状态 |
| `holdsPost` | id, postId | `WorldSlots` |
| `hasItem` / `characterHolds` / `hasBook` | itemId ／ id, itemId ／ id, bookId | 玩家库存／人物持有 |
| `reputationAtLeast` | value | Alpha 已有的声望值 |
| `eventSeen` | eventId | 玩家亲历过某 HISTORY／逸话事件 |
| `battleObjective` | objectiveId | **仅 `fulfillment` 段允许**（V-02） |

### 4.5 `RescueCase`（救人条件）

```json
{
  "id": "rescue.guan_yu.lb5.N1", "characterId": "guan_yu", "subject": "npc",
  "volume": "lb5", "node": "lb5.N1",
  "historicalFate": "219 年败走麦城，于临沮被擒杀", "source": "【史实】《三国志·蜀书·关羽传》；突围为【原创 if】",
  "hint": { "fromStage": "lb4.N9", "level": "rumor",
            "condition": { "favorAtLeast": ["guan_yu", 40] }, "textKey": "hint.rescue.guan_yu" },
  "eligibility": { "all": [ { "favorAtLeast": ["guan_yu", 60] }, { "flag": "shangyong_relief_ready" } ] },
  "fulfillment": { "all": [ { "battleObjective": "maicheng_breakout" } ] },
  "sceneVariant": { "kind": "battle", "default": "battle_maicheng", "eligible": "battle_maicheng_rescue" },
  "onSuccess": {
    "liftAnchor": "dies", "tierAfter": "T1S", "afterRouteId": "guan_yu.lb5.survivor",
    "unchangedDeaths": ["guan_ping", "zhao_lei"],
    "survivorConstraints": { "factionsAllowed": ["liu_bei", "wanderer"], "cannotHoldPosts": ["governor_jingzhou"], "mustNotBePresentAt": [] },
    "powerTransfer": null,
    "affectedNodes": [ { "node": "lb5.N3", "dialogueVariant": "yiling_guanyu_alive" } ],
    "epilogueVariantId": "ep.guan_yu_alive"
  },
  "onFail": { "historical": true,
              "memoryBroadcast": [ { "who": "zhang_fei", "key": "guanyu_death", "importance": 5 } ] },
  "canonLevel": "Provisional"
}
```

| 字段 | 说明 |
| --- | --- |
| `subject`* | `npc` 或 `protagonist`（叙事总监 B-7）。主角案例（孙策）的 `hint.condition`／`eligibility` 不得使用对主角求值的 `favorAtLeast`／`trustAtLeast`／`viewAtLeast`，只能用 `flag`／`eventSeen`／`hasItem`／`relationAtLeast(护卫人物, 主角)`；`onSuccess` 增加 `protagonistAfter`（东吴线止于 200 时填 `null`，由尾声变体承接；终章东吴方仍为周瑜） |
| `hint` | 线索从 `fromStage` 起、满足 `condition` 时进入天下传闻／酒馆情报。**文案只述现状、不述结果**（“吕蒙在陆口称病”可以，“日后恐有麦城之厄”不可以）；该案例的死亡地名人名进入未来词表直到死亡节点（叙事总监 B-10） |
| `eligibility`* | 节点剧情开始前（战斗或过场加载前）判定；成立则加载 `sceneVariant.eligible`（G-11） |
| `fulfillment`* | 场景结束时判定；只在 eligible 变体中评估。病死类案例（郭嘉、周瑜）`sceneVariant.kind` 为 `dialogue`，`fulfillment` 可为纯条件（B-9） |
| `tierAfter`* | 恒为 `T1S`：此人之后没有史实锚点，只受 `survivorConstraints` 约束，H＝0 |
| `unchangedDeaths`* | 同死者名单（关平、赵累；典韦案例的曹昂、曹安民）；校验器要求这些人物在同节点仍有 `dies` 锚点（B-8） |
| `powerTransfer` | `powerPositions` 非空时必填：`{ "position": "lord_jiangdong", "successor": "sun_quan", "mode": "retires_wounded", "atNode": "sc1.N6" }`；在 `atNode` 的结算第①步应用，之前各节点的 `WorldSlots.historical` 校验按节点逐点执行（F-09） |
| `affectedNodes`* | 可跨篇章（典韦、孙策、郭嘉指向共同终章）；校验器按全部篇章加载（F-09）。每人一段可插入台词，不为组合写剧情 |

规则：事件结果不变是校验器的硬检查——救人成功后的 `WorldSlots`、`historical=true` 的剧情标志、`unchangedDeaths` 的 `dies` 锚点，必须与失败路径完全相同。

### 4.6 `CharacterState`（状态层，写入存档）

```json
{
  "id": "zhao_yun", "tier": "T1", "alive": true, "pendingDeath": false,
  "location": "pingyuan", "faction": "gongsun_zan", "role": "刘备主骑",
  "recruitState": "inParty", "scriptedParty": true,
  "relations": {
    "liu_bei":     { "favor": 42, "trust": 30, "tags": ["主骑"], "baseline": { "favor": 0, "trust": 0 },
                     "views": { "benevolence": 60, "valor": 25, "wisdom": 10, "faith": 40, "authority": 5, "deceit": 0 } },
    "gongsun_zan": { "favor": 10, "trust": 40, "tags": [], "baseline": { "favor": 10, "trust": 40 } }
  },
  "heldItems": [ { "itemId": "book_sunzi_1", "origin": "player_given" } ],
  "memories": [ "…MemoryEntry…" ],
  "stageSummaries": { "lb1.N5": { "textKey": "sum.zhao_yun.lb1.N5.v2", "params": {} } },
  "fateHistory": [ { "stage": "lb1.N7", "optionId": "zhao_yun.lb1.follow_liubei_nominal", "cause": "DEFAULT",
                     "score": 30540, "default": true, "arbitrated": false } ],
  "rescues": {}
}
```

- `relations`* 以人物 id 为键；**对主角的态度就是 `relations[protagonistId]`**，`views` 只对主角必填（G-05）。`baseline` 记录 `initialRelations` 初值，供 `RELATION` 令牌用“相对上升量”判断（F-03b）。
- `heldItems[].origin`：离队／改投时 `player_given` 全部退回玩家库存，`own` 随人走（G-20）。
- `fateHistory[].cause`：多条候选记忆时取 importance 最高、其次 seq 最新的那条（F-03）；`RELATION:<id>` 作因时人物志写出那个人（“因与子龙之谊”，叙事总监 D-14）。
- `score` 为 Q16.16。

**`MemoryEntry`**（0.2 最小版的超集，G-04）：

```json
{ "id": "m_000192", "who": "lu_zhi", "key": "luzhi_cart", "value": "B_escort",
  "kind": "PLAYER", "node": "lb1.N1", "seq": 192,
  "actors": ["liu_bei", "lu_zhi"], "location": "guangzong_road",
  "tags": ["keep_promise", "courtesy"], "intensity": 2, "valence": 1, "importance": 3,
  "templateId": "mem.luzhi_cart", "params": { "choice": "B_escort" },
  "text": "卢植记得：刘备护送他到驿亭", "compressed": false }
```

| 字段 | 说明 |
| --- | --- |
| `who`*, `key`*, `value`, `node`*, `text`* | 与 0.2 完全相同；**同 who＋key 覆盖**；`memory(who,key)` 查询不变 |
| `kind`* | 0.2 的记忆按写入表逐条标 `PLAYER` 或 `WITNESS` |
| `seq`* | 全局递增，计数器存在 `WorldFateState.seqCounter`（F-13） |
| `tags`, `intensity`(1–3), `valence`(−2～2), `importance`(1–5) | 态度公式输入（§5.1）。0.2 迁移：有选择的写入点 importance 3，`seen` 类（无选择、无条件写入）importance **2**（F-03a），intensity 1，valence 0 |
| 无 `key` 的类型 | HISTORY 用 `h.<eventId>`，RANDOM 用 `r.<eventId>`，RUMOR 用 `ru.<stage>.<n>` 作 key |

### 4.7 `WorldFateState` 与 `WorldSlots`

```json
{
  "volume": "lb1", "protagonistId": "liu_bei", "stageId": "lb1.N7",
  "fateSeed": "0x9E3779B97F4A7C15", "fateDataVersion": 3, "seqCounter": 412,
  "pendingRecompute": false, "pendingTargetNode": null,
  "pendingTransitions": [ { "characterId": "tian_yu", "optionId": "tian_yu.lb1.leave_party",
                             "announcedStage": "lb1.N7", "commitStage": "lb1.N8", "cause": "PLAYER:m_000311",
                             "warningShown": true, "cooldownUntil": null } ],
  "deviationBudget": { "stage": "lb1.N7", "max": 4, "used": 1 },
  "slots": { "posts": { "governor_pingyuan": "liu_bei" }, "garrison": { "pingyuan": ["tian_yu", "jian_yong"] } },
  "rescueResults": {},
  "historyDelivered": { "lb1.N6": ["X10", "X9"] }
}
```

`WorldSlots` 数据（`world_slots.json`，只读；严纲按史实在界桥（192，`lb1.N6`）阵亡，用 `dies` 锚点而非底线名单——叙事总监 A-2）：

```json
{ "posts": [ { "id": "governor_pingyuan", "city": "pingyuan", "unique": true, "historical": { "lb1.N6": "liu_bei" } } ],
  "cities": [ { "id": "pingyuan", "region": "qingzhou", "garrisonCap": 6 } ],
  "factionFloors": [ { "faction": "gongsun_zan", "until": "lb1.E", "mustKeep": ["gongsun_zan", "tian_kai"] } ] }
```

`cities[].region` 同时是 `scope: region:<r>` 分发用的地点→地区表（V-01 纳入）。校验：`mustKeep` 中人物在 `until` 之前不得有 `dies` 锚点（V-06）。

**`StageEventLog`**（当前阶段，只追加）：`[{ "seq": 201, "type": "memory|recruit|gift|battle|flag|rescue", "scripted": false, "payload": {...} }]`。阶段内写记忆＝追加 `memory` 事件并立即投影；结算只读日志并回放全部事件（含 `recruit`），不读投影。

### 4.8 `TextEntry`（文本库，只读，开发期生成）

```json
{ "key": "dx.zhao_yun.wander_changshan.lb2a.close.*.liu_bei.v1",
  "slot": "dongxiang", "characterId": "zhao_yun", "optionId": "zhao_yun.lb2.wander_changshan",
  "stageGroup": "lb2a", "favorBand": "close", "viewDim": "*", "protagonistId": "liu_bei", "variant": 1,
  "text": "常山{p:zhao_yun}辞别{p:gongsun_zan}，回乡招募乡勇，自言“但从仁政所在”。",
  "placeholders": ["zhao_yun", "gongsun_zan"],
  "review": { "state": "approved", "reviewer": "bohua", "date": "2026-11-02", "batch": "lb2-b03" },
  "source": "llm:claude-sonnet-5-5:prompt_v4", "supersedes": null }
```

- 查表键 `(slot, characterId, optionId|'*', stageGroup|'*', favorBand, viewDim|'*', protagonistId|'*')`。
- `stageGroup`（叙事总监 F-22）：候选跨多个阶段且期间世界状态有变（阵营灭亡、人物死亡、称号变化）时，生成工具按 `history_events` 的 `splitsTextGroup=true` 事件把 `stages` 切成组；每组单独生成文本，保证不提到已死之人、未得之号。
- 示例改正：《云别传》“从仁政所在”是赵云答公孙瓒，不指名刘备（叙事总监 A-5）。这类史实错位 lint 查不出来，列入黄金样例的反例。

### 4.9 `ChronicleRecord`（跨篇章通关记录）

```json
{ "schemaVersion": 1, "line": "lb", "volume": "lb5", "protagonistId": "liu_bei",
  "runId": "a3f1…", "completedAt": "2027-03-01T12:00:00Z", "fateDataVersion": 3,
  "rescues": { "rescue.guan_yu.lb5.N1": "success" },
  "epilogueVariants": ["ep.guan_yu_alive"],
  "characters": {
    "guan_yu": { "touched": true, "owner": true,
                 "whereabouts": { "alive": true, "tier": "T1S", "faction": "liu_bei", "location": "chengdu", "recruitState": "inParty", "finalOption": "guan_yu.lb5.survivor.stay" },
                 "relationToProtagonist": { "favor": 92, "trust": 95, "views": {} },
                 "coreMemories": [ "…importance 5 的 MemoryEntry…" ] },
    "cao_cao":  { "touched": true, "owner": false,
                 "relationToProtagonist": { "favor": -30, "trust": 10, "views": {} }, "coreMemories": [] }
  },
  "finaleInputs": { "chibi": { "partyCandidates": ["guan_yu", "zhang_fei", "zhao_yun"] } }
}
```

每条线导出**所有“触碰过”的人物**（有任何记忆或关系变化者）：`owner=true` 者带 `whereabouts`，所有人都带 `relationToProtagonist` 与核心记忆（技术总监 F-07）。组装规则见 §7.3。

### 4.10 与兵书／宝物／装备的字段对齐表

| 事项 | 兵书 GDD 条文 | v5.1 字段／规则 |
| --- | --- | --- |
| 五行适性 | §3.7 S/A/C，恰好 1 S、≥1 C | `CharacterProfile.wuxingAptitude`；校验 V-07（归兵书系统，兵书沙盒实装后启用） |
| 适性变化 | §3.7 只走预设分支 | `effect.aptitudeChange` 由数据写死 |
| 偏好书 | §3.11 赠偏好书额外加好感 | `gift_book` 且书在 `favoredBooks` 时 **Δfavor ×1.5**（作用于结果，不作用于 intensity，F-10） |
| 赠宝对傲者 | — | `gift_treasure` 对“傲”的 +3 只在宝物与该人物 `favoredBooks` 或兵器相关时生效，否则 0（叙事总监 E-20）；宝物数据加 `affinityCharacters` |
| 赠书退回 | §3.11 离队或改投退回 | `heldItems[].origin == player_given` 全部退回 |
| 敌将缴获书 | §3.10 若改投则“招降时携带” | `effect.itemTransfer` 不得移除 `own` 书 |
| 兵书不改命运 | §3.11 | 引擎不读兵书栏；`hasBook` 只用于条件 |

---

## 5. 运行时流程

### 5.1 阶段内：交往即时影响（确定性，全程 Q16.16）

每条 PLAYER 记忆写入时，立即更新 `relations[protagonistId]`。所有运算用 Q16.16 整数，中间值 `long`，最终 `>>16` 得整数结果前按 §5.3 的取整规则（四舍五入，半值远离零）。

```
Δview[d]  = Σ_t  TAG_TO_VIEW[t][d] × intensity
Δfavor    = Σ_t  intensity × react(t)  +  valence × intensity × 2
react(t)  = Σ_k  (personality_k − 50) / 50 × REACT[t][k]        （k 遍历八维，全部参与求和）
Δtrust    = Σ_t  TRUST_DELTA[t] × intensity
```

限幅：favor ∈ [−100,100]，trust、view ∈ [0,100]。偏好书：`gift_book` 且书在 `favoredBooks` 中时，Δfavor 乘 1.5 后取整（F-10）。

**TAG_TO_VIEW 初值**（每点 intensity）：

| tag | 仁德 | 勇武 | 智略 | 信义 | 威严 | 诡诈 |
| --- | --- | --- | --- | --- | --- | --- |
| benevolence | +6 | | | +1 | | |
| valor | | +6 | | | +2 | |
| wisdom | | | +6 | | | +1 |
| keep_promise | | | | +6 | | |
| break_promise | | | | −10 | | +4 |
| authority | | +1 | | | +6 | |
| deceit | | | +2 | −4 | | +8 |
| courtesy / insult | | | | +1 / −1 | | |
| gift / gift_book / gift_treasure | +1 | | | | | |
| fought_alongside | | +2 | | +2 | | |
| fought_against / captured / spared | | +1 / +1 / 0 | | 0 / 0 / +2 | 0 / +2 / 0 | |

**REACT[t][k] 初值**（−10～+10；列：忠 义 仁 勇 谋 名 利 傲；按叙事总监 E-17～E-20 修正）：

| tag | 忠 | 义 | 仁 | 勇 | 谋 | 名 | 利 | 傲 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| benevolence | 0 | +3 | +8 | 0 | 0 | 0 | −2 | 0 |
| valor | 0 | +2 | 0 | +8 | −1 | +2 | 0 | +1 |
| wisdom | 0 | 0 | 0 | −1 | +8 | +1 | 0 | +1 |
| keep_promise | +4 | +8 | +2 | 0 | 0 | 0 | 0 | 0 |
| break_promise | −6 | −10 | −3 | 0 | 0 | 0 | 0 | 0 |
| authority | +3 | 0 | −2 | +2 | 0 | +2 | 0 | −2 |
| deceit | −3 | −8 | −4 | 0 | +3 | −2 | +2 | −1 |
| courtesy | +2 | +3 | +2 | 0 | +2 | +3 | 0 | +6 |
| insult | −2 | −3 | −1 | +1 | −2 | −4 | 0 | −10 |
| gift | 0 | +1 | 0 | 0 | 0 | +2 | +6 | 0 |
| gift_book | 0 | +1 | 0 | 0 | +5 | +2 | +1 | +2 |
| gift_treasure | 0 | +1 | 0 | +2 | 0 | +4 | +5 | +3（条件见 §4.10） |
| fought_alongside | +4 | +6 | 0 | +4 | 0 | +1 | 0 | 0 |
| fought_against | −2 | −2 | 0 | +2 | 0 | 0 | 0 | +1 |
| spared | 0 | +6 | +5 | 0 | 0 | 0 | 0 | −3 |
| captured | −2 | −3 | 0 | 0 | 0 | 0 | 0 | −6 |

**TRUST_DELTA 初值**：keep_promise +6、fought_alongside +4、spared +3、courtesy +1、gift 系 +1、break_promise −10、deceit −6、insult −2、captured −3，其余 0。

例（八维全算，叙事总监 A-4）：赵云（忠 85 义 90 仁 80 勇 85 谋 45 名 30 利 15 傲 35）遇一次 intensity 2、valence −1 的 deceit：
react ＝ 0.7×(−3) ＋ 0.8×(−8) ＋ 0.6×(−4) ＋ 0.7×0 ＋ (−0.1)×3 ＋ (−0.4)×(−2) ＋ (−0.7)×2 ＋ (−0.3)×(−1) ＝ −2.1 −6.4 −2.4 +0 −0.3 +0.8 −1.4 +0.3 ＝ **−11.5**；
Δfavor ＝ 2×(−11.5) ＋ (−1)×2×2 ＝ −23 − 4 ＝ **−27**；Δtrust ＝ −12；诡诈 +16、信义 −8、智略 +4。
对照荀彧型（忠 95 义 85 仁 70 谋 90 名 60）：react ≈ −2.7 −5.6 −1.6 +2.4 −0.4 ≈ −7.9，仍为明显负值；贾诩型（义 40 谋 95 名 20 利 60）：≈ +1.6 +2.7 +1.2 +0.4 ≈ +5.9，正值——“诈”对高谋低义者加分、对高义者大减的要求成立。

### 5.2 进入节点：结算八步（修订版）

```
① 阶段结算   回放日志（记忆、recruit、gift、battle、flag、rescue）；并入 pendingDeath / rescueResults / powerTransfer；分发 HISTORY（§5.5）；抽 RANDOM；写 RUMOR
② 记忆整理   压缩（§5.6），更新 stageSummaries
③ 候选生成   每人取下一阶段候选 → 在队范围限制（§4.3）→ HARD 锚点过滤 → preconditions 过滤 → 偏离需因过滤
④ 打分抽签   公式 §5.3（定点数）→ 每人一个“拟选”
⑤ 两步提交   twoStep 候选：首次胜出只写 pendingTransitions（宣告），本阶段走默认候选；已宣告者在提交阶段不重抽，只复核阈值
⑥ 仲裁       偏离预算、唯一官职、驻军上限、阵营底线、唯一宝物、互投；全序 (tierWeight↓, score↓, characterId↑)
⑦ 提交       应用 effect；写 fateHistory；HARD 锚点与 WorldSlots.historical 复核；SettleResult.Changes 交 Gameplay 在同一事务内应用到库存与队伍；整档原子写
⑧ 呈现       天下动向、人物志、酒馆情报：按 §5.7 查文本库
```

**第③步“偏离需因”（G-06，按 F-03 收紧）**。先收集该人物的因果令牌集合 C：
- `PLAYER:<tag>`：存在 kind=PLAYER、importance ≥3、带该 tag 的记忆（任何阶段；核心记忆长期有效）。同时派生 `PLAYER:+`（valence>0）或 `PLAYER:-`（valence<0）；
- `RANDOM:<id>`：第①步抽中的随机事件，且 `causesDeviation=true`；令牌**随 `pendingTransitions` 一起保存**到提交阶段（F-04a）；
- `HISTORY:<id>`：本阶段分发给此人的史事，且 `causesDeviation=true`；
- `RELATION:<charId>`：对某人好感比 `baseline.favor` **上升** ≥ `relationCauseThreshold`（默认 40）（F-03b）。
非默认候选 o 进入打分的条件：`o.requiresCause ∩ C ≠ ∅`。默认候选总是进入。“全不交互”时只有 RANDOM／HISTORY 带因的候选能偏离，数量由数据控制。`fateHistory.cause` 记录命中的令牌（多条时按 §4.6 规则取一条）。

**第⑤步两步提交（G-12、G-22，按 F-04 修正）**：
1. `twoStep=true` 的候选首次胜出：不执行；写 `pendingTransitions{ cause, announcedStage, commitStage=下一阶段 }`；本阶段人物走默认候选；阶段内触发一次预警对白（候选数据 `warningSceneId`）。
2. 提交阶段（`commitStage`）的结算：该人物**不重抽**；免除第③步的因过滤（令牌已随条目保存），只复核 `preconditions` 与阈值（如 `leaveParty` 的 favor/trust 阈值）。仍满足 → 执行；不满足 → 撤销，写一条 RANDOM 类记忆“打消了念头”，并设 `cooldownUntil = 再下一阶段`，冷却期内该候选不得再宣告（防止“狼来了”震荡）。
3. V-05 校验 `twoStep` 候选的 `stages` 含紧接的下一阶段。
4. T3 `dies` 候选的 `requiresCause` 必须含某个 `RANDOM:<id>` **或** `HISTORY:<id>`（原创人物随某场史实战役在场）；此外剧情可以不经引擎直接写入 T3 死亡（支线失败的后果），这是叙事总监建议保留的第三条路（D-16）。

**第⑥步仲裁全序**：`tierWeight` T0=4、T1=3、T1S=3、T2=2、T3=1。偏离预算 N ＝ ceil(在场 T1/T2 人数 × `deviationBudgetRatio`)，“在场”按 §3.3 定义；被预算挤回默认的人写 `arbitrated=true`。互投按全序先提交者为准，后者重新过滤。

**第⑦步复核失败**（理论上不应发生）：回退该人物到默认候选并记错误；CI 模拟器视为数据错误。

### 5.3 打分与确定性

```
score(o) = wP·fitP(o) + wA·fitA(o) + wR·pull(o) + wI·prox(o)·infl + H[tier]·[o 为默认] + ε
P(o) = softmax(score / τ)
```

| 项 | 定义 | 范围 |
| --- | --- | --- |
| fitP | 候选 `tags` 向量 t 与 p＝(personality−50)/50 的余弦：`cos = dot(t,p)·65536 / (isqrt(dot(t,t))·isqrt(dot(p,p)))`（Q16.16，`long` 中间值，分母为 0 时 cos=0），再映射 `fitP = (cos + 65536) >> 1` | 0–1 |
| fitA | Σ 被 `ambitionsServed` 覆盖的抱负千分权重 × 65536 / 1000 | 0–1 |
| pull | 候选目的地阵营／城市中，与此人好感最高者的 favor × 65536 / 100 | −1～1 |
| infl | clamp((favor+trust) × 65536 / 200, −65536, 65536)，取对主角 | −0.5～1 |
| prox | `playerProximity` | −1,0,1 |
| H[tier] | T0 不打分（只有默认）；T1 0.35；T2 0.25；T3 0；T1S 0 | — |
| ε | 每人独立随机流抽取的均匀扰动，默认 ±0.05 | — |

初值：wP 0.25、wA 0.20、wR 0.20、wI 0.35、τ 0.10。校核：T1 人物在 infl=1、prox=+1 时玩家影响 0.35 恰与 H 相抵，去向由性格／抱负契合决定。

**定点数规格（G-23，按 F-01 修正）**：
- 所有分值为 Q16.16 `int`；**所有乘除的中间值用 `long`**，乘后 `>>16`（算术右移＝向下取整），除法前先左移 16。规格内凡写“取整”均指“向下取整（`>>`）”，除 §5.1 最终 Δ 与 §5.3 ε 之外不出现 `/` 截断；需要四舍五入处写明 `(x + 32768) >> 16`。
- softmax 索引：`d_i = score_i − score_max`（≤0，Q16.16）；`idx_i = clamp( (int)((long)(−d_i) × 256 × 65536 / τ >> 16), 0, 4096 )`，即以 1/256 为步长的 exp 自变量索引（避免 `int` 溢出，F-01）；`w_i = EXP_TABLE[idx_i]`（线性插值用 `long`，结果 `>>16`）。`EXP_TABLE` 为 4097 项 Q16.16，覆盖 exp(−x) 于 x ∈ [0,16]，`EXP_TABLE[0]=65536` 保证总和 >0；随包发布，V-10 校哈希。
- 抽签：`r = draw.Next() % Σw_i`（`Σ` 用 `long`），按候选 id 序数字典序累加比较。
- **随机流表（F-02）**。PCG32（Melissa O'Neill 标准参数：乘数 6364136223846793005，increment ＝ `(stream << 1) | 1`，输出 XSH-RR）。64 位哈希：FNV-1a 64 对字符串的 **UTF-8 字节**；SplitMix64 指其 **finalizer**（`z=(x+0x9E3779B97F4A7C15); z=(z^(z>>30))*0xBF58476D1CE4E5B9; z=(z^(z>>27))*0x94D049BB133111EB; z^(z>>31)`）。

| 流 | 种子 | 用途 |
| --- | --- | --- |
| `settleSeed` | SplitMix64(fateSeed ^ FNV1a64(nodeId)) | 派生下列各流 |
| `random` | PCG32(state=SplitMix64(settleSeed ^ FNV1a64("random")), stream=FNV1a64(characterId)) | 第①步随机事件抽取，每人按 id 序各抽一次 |
| `eps` | PCG32(…FNV1a64("eps")…, stream=FNV1a64(characterId)) | 第④步 ε |
| `draw` | PCG32(…FNV1a64("draw")…, stream=FNV1a64(characterId)) | 第④步 softmax 抽签 |
| `text` | PCG32(…FNV1a64("text")…, stream=FNV1a64(characterId ^ slot)) | 第⑧步文本 variant |

- 遍历顺序：人物 id、候选 id 均按 `string.CompareOrdinal`。
- JSON 中的所有数值为整数（§4 开头）；加载层禁止 `double`。
- 禁止 `UnityEngine.Random`、`System.Random`、`float`/`double`、依赖 `Dictionary`/`HashSet` 枚举顺序。

### 5.4 救人判定流程

```
节点剧情开始前 → 对本节点每个 RescueCase：
   eligible = eval(eligibility)         ← 只读快照与剧情标志
   加载 sceneVariant[eligible ? "eligible" : "default"]（战斗或对话/过场变体）
   … 场景 …
场景结束 → 若 eligible：success = eval(fulfillment)
        → rescueResults[caseId] = success ? "success" : "fail"（写日志，随下次提交入档）
        → 成功：播放 onSuccess 过场，pendingDeath=false；失败：按 historical 继续
下一节点结算第①步 → 成功者：tier=T1S，移除 dies 锚点，路线替换为 afterRouteId；unchangedDeaths 照常；
                     失败者：alive=false，memoryBroadcast
powerTransfer 在其 atNode 的结算第①步应用（F-09）
```

战败重试还原战前快照，`rescueResults` 在场景结束时才写，重试不留痕迹。

### 5.5 知识隔离：HISTORY 分发规则（G-15）

`history_events.json`：`{ "id": "X10", "stage": "lb1.N6", "scope": "world", "delayStages": 0, "causesDeviation": false, "splitsTextGroup": false, "memory": { "key": "h.X10", "importance": 3, "templateId": "mem.h.X10" } }`。

| scope | 谁收到 | 何时 |
| --- | --- | --- |
| `world` | 全部存活人物 | 阶段 stage + delayStages 的结算第①步 |
| `region:<r>` | 结算时 `location` 所属地区（`cities[].region`）为 r 者 | 同上 |
| `faction:<f>` | `faction == f` 者 | 同上 |
| `explicit:[ids]` | 列出者 | 同上 |

数据约定：跨州事件默认 `delayStages ≥ 1`（叙事总监 F-23）。人物只会收到 `stage ≤ 当前阶段` 的事件——“绝不把未来写进记忆”的实现。`historyDelivered` 防重复。

### 5.6 记忆压缩

1. 未压缩、importance ≤3 的条目超过 `memoryCap`（默认 40）时，按 `seq` 从旧到新压缩超出部分；
2. importance 4 的条目在写入 3 个阶段后才可压缩；importance 5 永不压缩；
3. 压缩＝标记 `compressed=true`（保留 key/value 供 `memory()` 查询，`text` 置空）并把 tags 计入阶段摘要；
4. 摘要由模板生成：取该阶段 tags 频次最高的 3 类，拼 `sum.<tier>.<tagA>.<tagB>.<tagC>`；文本库有 `summary` 条目则优先。

### 5.7 文本选取与回退链

1. `(slot, char, option, stageGroup, band, dominantView, protagonist)`
2. `(slot, char, option, stageGroup, band, *, protagonist)`
3. `(slot, char, option, stageGroup, band, *, *)`
4. `(slot, char, option, 相邻 stageGroup, band, *, *)`（只在该组无“已死／称号”变化时允许，由生成工具标 `compatibleGroups`）
5. `(slot, char, *, *, band, *, *)`（人物通用）
6. `(slot, generic.<optionType>, *, *, band, *, *)`
7. 代码内模板 `summaryTemplate`

`dominantView` ＝ 六看法中最高且 ≥ 50 的维度，否则 `*`。`reunion` 槽位取该人物对主角 importance 最高、其次 seq 最新的 PLAYER 记忆，用其 `templateId` 对应的 `reunion` 文本；没有则 `reunion.generic.<band>`。多 variant 用 `text` 流选。占位符 `{p:id}`、`{l:id}` 缺 id 时记错误并显示 id。

---

## 6. 开发期内容管线（大模型生成＋人工审核）

### 6.1 产物

| 产物 | 由谁产 | 审核 | 数量控制 |
| --- | --- | --- | --- |
| 候选库、锚点、默认路线、救人案例、history／random 事件、WorldSlots | 策划撰写；可用模型起草 `Provisional` 稿 | 叙事总监 ND-CONSISTENCY 逐条核验出处 | T1 每阶段 ≤3 候选；T2 用通用候选；每篇章救人 ≤2 例 |
| 文本库（六槽位） | 模型批量生成 | **人工全审** | 每篇章 ≤5,000 条待审文本（含 variant）为硬上限；实际配额＝校准后反推（§6.6） |
| 存活版对白、尾声变体、预警对白、离队剧情、救人线索 | 写手撰写（Yarn） | ND-CONSISTENCY | 每个救人案例：每个 affectedNode 一段 |
| 记忆模板 `mem.*` | 策划撰写 | 文案审 | 每个写入点一条 |

### 6.2 生成集合与优先级

生成集合 G 由工具从模拟器可达性报告（§8.2）算出：

```
G = { (char, option, stageGroup, band) | char.recruitableIn ∋ volume 或 char 在本篇章有 ≥1 个写入点,
                                          option 在可达报告中到达过, band ∈ {close, neutral, distant} }
```

槽位优先级（预算不足从后往前砍，砍掉的走通用模板）：`reunion`（每人每 band 2 variant）＞ `dongxiang` ＞ `hint` ＞ `rumor` ＞ `monologue` ＞ `summary`。工具输出“本篇章预计待审条数（含 variant）”报告，producer 批准后才开生成。

第一篇章粗估（制作人意见 8 修正）：约 40 名可招募或有写入点的人物 × 平均 2 个可达候选 × 3 档 ＝ 240 个键；最小集（reunion 40×3×2 ＋ dongxiang 240）≈ **480 个键**，全六槽位 ≈ 1,400 个键；按 n=2 variant 生成，**待审文本约 960（最小集）～2,800（全槽位）条**。以下凡“条”均指待审文本（含 variant）。

### 6.3 流水线

```
① 数据校验（§8.1 全过）
② 导出批次：tools/fate-pipeline export --volume lb1 → batches/lb1-b01.jsonl
③ 调用模型（批处理接口，温度 0.7，n=2）→ generated/lb1-b01.jsonl
④ 自动 lint（见下表）
⑤ 人工审校（表格视图，每条 通过／改写／驳回＋理由）
⑥ 入库：approved 合并进 text_library/<volume>.json；id 不可变；改写产生新 variant，旧条目 supersedes
⑦ 回归评测：50 条黄金样例（含“赵云语安到刘备头上”这类史实错位反例）
⑧ 本地化：只对 approved 做；占位符原样保留
```

**lint 项**（叙事总监 F-24 补齐）：

| # | 检查 | 数据来源 |
| --- | --- | --- |
| L1 | JSON 合法、字数上限（dongxiang ≤40、rumor ≤40、monologue ≤60、reunion ≤80、summary ≤150、hint ≤50） | — |
| L2 | 占位符合法且在上下文名单内 | 批次上下文 |
| L3 | 非占位真名黑名单（全三国人名地名库）；**史料原句引用白名单**不拦 | `lint/names.json`、`lint/quotes_allow.json` |
| L4 | 按阶段的未来事件与国号词表（含各救人案例的死亡地名人名直到死亡节点） | `lint/future_terms/<stage>.json` |
| L5 | 按阶段的已死人物名单（不得写成在世） | 从 `dies` 锚点自动生成 |
| L6 | 按阶段的称谓／官职表（皇叔、使君、丞相各有起始年份；PROJECT §2） | `lint/titles.json` |
| L7 | 地名年代表（许都 196 前、建业 229 前等） | `lint/place_eras.json` |
| L8 | 神怪／穿越词表 | `lint/forbidden.json` |
| L9 | `voice.selfRef` 与 `taboo` | 档案 |
| L10 | 重复率、禁用词 | — |

状态机：`generated → lint_failed | lint_passed → in_review → approved | rejected(reason) → localized`。只有 `approved`／`localized` 进包。

### 6.4 上下文包与输出格式

输入：固定系统提示（世界规则、时代用语规范、禁用事项、输出格式）＋人物档案摘录＋候选摘要＋阶段组的世界状态摘要（谁在世、谁是谁的主君、当前称谓）＋好感档描述＋允许提及的人名地名清单＋该阶段未来词表（禁用）。输出严格 JSON：`{ "variants": [ { "text": "…", "placeholders": ["…"] } ] }`。模型**不**输出选择、数值、标签。

### 6.5 模型可以做／不可以做

| 可以 | 不可以 |
| --- | --- |
| 写文本库各槽位的候选文案 | 决定任何人物去向、数值、锚点 |
| 起草 `Provisional` 候选与锚点供策划改 | 把 `Provisional` 改成 `Established` |
| 做一致性自检 | 作为最终审核 |
| 起草史料出处清单 | 替代人工核验出处 |

### 6.6 成本与工时（开发期，制作人意见 9）

| 项 | 估算 | 依据 |
| --- | --- | --- |
| 模型调用 | 5,000 条 ×（输入 ≈1,500 token ＋ 输出 ≈120 token）≈ 7.5M 输入 ＋ 0.6M 输出；Claude Sonnet 5.5 批处理约 10–15 美元／篇章；含三轮重生成 < 60 美元／篇章 | 官方价目以立项时为准 |
| 人工审校 | **每条 30 秒～3 分钟**，P1 首批 **100 条校准批**实测后定；按 2 分钟计 5,000 条 ≈ 170 小时，因此 5,000 只是硬上限，**实际配额＝40 小时预算 ÷ 实测分钟数** | 校准前不批准任何篇章配额 |
| 候选与锚点撰写 | 单人单阶段工时在 P0 实测，乘以 P0.5 名单人数后进 PR-SPRINT | v4 风险表 |

---

## 7. 存档

### 7.1 `FateSaveSection`

```json
{ "schemaVersion": 1, "fateDataVersion": 3,
  "world": { "…WorldFateState…" },
  "characters": { "zhao_yun": { "…CharacterState…" } },
  "eventLog": [ "…当前阶段的 StageEventLog…" ],
  "fateCommitted": "lb1.N7" }
```

- 0.2 记忆最小版迁移：**随 0.4 的存档版本升级一次完成**（P0b），不在 0.3 期间单独升版（制作人意见 7）；0.3 的新写入点现在就在写入表里补 `kind/importance/tags` 三列（纯数据），迁移时表驱动。迁移规则见 §4.6（`seen` 类 importance 2）。`relations` 初始化为 `initialRelations`，`baseline` 同值；`fateSeed` 对旧档从存档创建时间派生一次。
- 体积：120 人 × 40 条 × ≈250 B ≈ 1.2 MB 原始，压缩后 ≈300 KB。
- `fateDataVersion` 升级：`fate_migrations.json` 列出 `optionId` 改名表；被删除的候选回退默认路线并写 HISTORY“去向不明”；锚点新增不追溯。`ChronicleRecord` 读取同样经此表（F-07）。

### 7.2 提交协议（G-17，按 F-05、F-06 修正）

1. **事务单位是整份存档文件**（命运段、库存、队伍、剧情标志一起），不是 `FateSaveSection` 单独提交：退回物品、`itemTransfer`、离队都在同一次写入里（F-05）。
2. 进入节点时先原子写一次“日志冻结 ＋ `pendingTargetNode` ＋ `pendingRecompute=true`”（§3.3 ⓪），再结算。
3. 结算在内存中完成；Gameplay 把 `SettleResult.Changes` 应用到库存与队伍；整份 `SaveDocument` 写临时文件 → `FileStream.Flush(true)` → `File.Replace` 原子替换自动档；成功后 `pendingRecompute=false`、`fateCommitted=stageId`。
4. 读档见 `pendingRecompute=true`：**从第①步整体重跑**（Settle 是纯函数，幂等；F-06）。
5. 没有缓存或待提交日志；没有外部调用，无需防刷。

**不可刷命运**：同一存档、同一阶段行为相同 → 结果逐字段相同。玩家要改变结果，只能改变阶段内的行为。

### 7.3 跨篇章 `ChronicleRecord`（G-18，按 F-07 修正）

- 写入时机：篇章结算画面与尾声变体播放后各写一次（后者覆盖前者）；挂在 0.5 的“篇章结算”故事上（制作人意见 11）。
- 位置：`Application.persistentDataPath/chronicle/<line>/<volume>/<runId>.json`；一个篇章可有多条记录。
- 读取方：下一篇章开档（同线）、共同终章（三线）。开档时默认取同线最新记录，可选其他记录；没有记录时用该线的**默认终局记录**（随包数据 `chronicle_defaults/<volume>.json`，按默认路线算出），保证“未通关也能玩”。
- **按字段分主组装**：去向类字段（`whereabouts`：alive/tier/faction/location/recruitState/rescues/finalOption）取 `ownerLine` 那条线的记录；`relations[protagonistX]` 取 X 线的记录（关羽对曹操的态度来自曹操线，关羽的生死来自刘备线）；核心记忆按来源线合并（键不冲突）。校验：救人案例的人物 `ownerLine` 必须等于案例所在线。
- 终章开档时把所选的三条记录**整份复制进终章存档**，之后不再依赖外部文件。
- 被救者 `tier=T1S` 按 `survivorConstraints` 放置并用存活版对白；不参与改变终章结果。早于 208 的救人（典韦、孙策、郭嘉）影响终章在场；晚于 208 的（周瑜、庞统、关羽）只影响各自篇章与尾声变体。

---

## 8. 工具、校验、测试

### 8.1 构建期校验（失败即构建失败；分级见右列，制作人意见 5）

| 编号 | 检查 | 期 |
| --- | --- | --- |
| V-01 | 所有 id 引用存在（人物、候选、节点、场景、标志、物品、书、地点、阵营、官职、地区）；**按全部篇章加载**（跨篇章 affectedNodes） | P0a |
| V-02 | 条件只用 §4.4 谓词；`battleObjective` 只出现在 `fulfillment` | P0a |
| V-03 | `activeFrom` 起每个（人物，阶段）恰好一条默认候选；默认候选无 `preconditions` 且满足该阶段 HARD 锚点 | P0a |
| V-04 | 每条 HARD 锚点至少有一个候选满足；与 `inParty` 冲突者有 `departureSceneId`，否则 `recruitableIn` 不含本篇章；与 `presentAt` 锚点冲突的候选不得出现在该阶段 | P0a |
| V-05 | 非默认候选 `requiresCause` 非空；因与 `playerProximity` 同向；`twoStep` 候选有 `warningSceneId` 且 `stages` 含紧接下一阶段 | P0a |
| V-06 | 救人：`powerPositions` 非空者有 `powerTransfer`；`subject=protagonist` 的条件不含对主角求值谓词；`unchangedDeaths` 在同节点有 `dies` 锚点；`affectedNodes` 变体存在；`mustKeep` 人物在 `until` 前无 `dies`；成功／失败路径 `historical` 一致（此项由模拟器断言） | P0a（静态部分）/ 模拟器 |
| V-07 | 五行适性恰 1 S、≥1 C | 兵书沙盒后 |
| V-08 | `history_events` 的 `stage` 属于本篇章且顺序单调 | P0a |
| V-09 | 文本库 lint L1–L10 | P1 |
| V-10 | `EXP_TABLE` 与调参表哈希与代码常量一致 | P1（P0 先编译进代码） |

### 8.2 可达性：两层（制作人意见 3）

- **静态层（P0a）**：V-03、V-04 的静态部分（每阶段有默认、每条 HARD 锚点至少一个候选、冲突者有离队剧情）。
- **模拟层（P0a）**：模拟器用“最大交往”策略跑 N 次，输出**可达性报告**：哪些 `recruitableIn` 人物从未到达 `available` 或剧情招募点、哪些救人案例的 `eligibility ∧ fulfillment` 从未同时成立，并列出缺口（缺哪个候选／离队剧情／标志）。报告有缺口 → 构建失败。
- 形式化证明（把条件语言全展开成图）降为 P2 或第二篇章立项时再议。

### 8.3 测试（按期；技术总监 F-12 拆性能预算）

| 类型 | 内容 | 通过标准 | 期 |
| --- | --- | --- | --- |
| 单元 | 态度公式、定点 softmax、随机流、条件求值、压缩、迁移、全序仲裁、两步提交 | 全过 | P0a |
| 整篇章模拟（EditMode，无头，不写存档） | P0a：1,000 次 × 2 策略（全不交互／最大交往）；P0.5 起 10,000 次 × 4 策略 | HARD 锚点违反 0；全不交互时按（人物，阶段）计偏离 ≤5%；预算从不超额；任何救人组合下 `historical` 与 `WorldSlots.historical` 不变；可推进到篇章末 | P0a / P0.5 |
| 回放 | 同存档同行为重放 | 逐字段一致（含文本键） | P0b |
| 跨平台确定性 | Mono 与 IL2CPP 同种子 1,000 次（随 0.5 的 IL2CPP 冒烟构建跑一次） | 结果哈希一致 | 0.5 |
| 文本回退 | 删空文本库 | 可通关，模板文本，无异常 | P1 |
| 性能 | 无 I/O 结算（120 人合成档案）≤ **5 ms**（CI 回归）；含整档写入 ≤ **500 ms**（Steam Deck 实测，2 s 为上限） | — | P0.5 |

### 8.4 验收标准（修订，G-19；归属见右列，制作人意见 6）

| # | 标准 | 期 |
| --- | --- | --- |
| 1 | 断网状态下，从篇章首玩到篇章末，`recruitableIn` 含本篇章的人物都能被招募，HARD 锚点全部成立 | P0.5 |
| 2 | 读档后不改变行为重新进入节点，所有人物去向与文本键完全相同 | P0a |
| 3 | 与某 T1 人物有 importance ≥3 的交往后，该人物至少一个同向非默认候选进入打分；无交往且无随机事件时，T1 候选集只有默认 | P0a |
| 4 | 重逢对白引用至少一条该人物与主角有关的记忆 | P1 |
| 5 | 两步提交：离队候选首次胜出时阶段内出现预警；玩家把好感拉回阈值以上，下次结算不离队；撤销后冷却一阶段 | P0a |
| 6 | 测试用救人案例：eligibility 不满足时无救人目标；满足并完成后人物以 `T1S` 出现在存活路线位置，`unchangedDeaths` 照常，`WorldSlots.historical` 与失败路径一致，affectedNode 播放存活版对白 | P0a |
| 7 | 删除文本库后游戏可通关 | P0.5 |
| 8 | 文本库 lint：500 条故障样本（真名、未来国号、已死人物、错位称谓、超长）100% 被拦截 | P1 |

---

## 9. 模块划分与接口（摘要，细节见 ADR-0013）

| 程序集（ADR-0001） | 内容 |
| --- | --- |
| `ThreeKingdoms.Core.Fate` | `FateEngine.Settle(FateDataSet, SaveDocument, StageEventLog, nodeId, IConditionContext) → SettleResult`；`FateEngine.ApplyInteraction(...) → AttitudeDelta`（不修改入参，由调用方应用）；`ConditionEvaluator`；`FixedMath`/`Pcg32`/`Hash64`；`Arbitrator`；`MemoryStore`；只读查询接口 `IFateQuery` |
| `ThreeKingdoms.Data.Fate` | JSON 加载为 `[Serializable]` 公有字段 DTO（ADR-0001 约定），再包成只读视图 `FateDataSet`；程序集引用 Newtonsoft（对 ADR-0001 依赖表的修订条款见 ADR-0013） |
| `ThreeKingdoms.Save` | `SaveDocument` 整档读写、迁移、原子提交；`ChronicleStore` |
| `ThreeKingdoms.Narrative` | Yarn 函数经 `IFateQuery`：`memory(who,key)`、`favor(who)`、`trust(who)`、`view(who,dim)`、`fate_location(who)`、`fate_faction(who)`、`in_party(who)`、`rescued(caseId)`；`TextLibrary.Lookup(TextQuery) → TextEntry?` |
| `ThreeKingdoms.Gameplay` | `StageDirector`、`RescueDirector`、`InteractionRecorder`（0.2 记忆写入扩展为事件日志） |
| `ThreeKingdoms.EditorTools.Fate` | 校验器、模拟器＋可达性报告、管线导出／导入、命运面板（EditorWindow：上次结算每人候选、分值、因果令牌、仲裁结果表格＋状态 JSON 导出） |

---

## 10. 分期（按 PR-SCOPE 修订）

| 阶段 | 内容 | 排期位置 | 退出标准 |
| --- | --- | --- | --- |
| **P0a 引擎核心** | `Core.Fate` ＋ `Data.Fate` ＋ EditMode 测试 ＋ 模拟器与可达性报告 ＋ 校验器 V-01/02/03/04/05/06(静态)/08；全部用合成测试数据（6–8 人、2 阶段、1 个仅测试数据的救人案例、1 个 twoStep 离队候选）；**不碰场景、存档与节点状态机** | **与 0.3 讨董并行**，独立云端线程 | AC 2、3、5、6 在测试数据上通过；模拟器 HARD 锚点违反 0；实测单人单阶段候选撰写工时 |
| **P0b 游戏内集成** | `StageDirector`／`RescueDirector`／`InteractionRecorder`；`FateSaveSection` 整档原子提交；0.2 记忆迁移（随 0.4 存档升版一次完成）；Yarn 函数 `memory/favor/trust/in_party/rescued`；命运面板；R4 渤海 1 区域、N5→N6→N7 两次结算、赵云／太史慈／田豫等真实数据 | **0.4 河北的第一个故事**（约 5–7 个工作日，计入 0.4 缓冲之外） | 回放一致；0.2 存档迁移后查询结果不变 |
| **P0.5 第一篇章命运数据** | 第一篇章约 40 人的 routes／anchors／world_slots／history_events／random_events；T0 只写默认路线，T1（≤10 人）每阶段 ≤3 候选，T2 全用 `generic.*`；`ChronicleRecord` 挂 0.5 篇章结算 | 0.4 中后段开始，0.5 前完成 T1 名单 | V-01…V-06、V-08 全过；可达性报告无缺口；AC 1、7；10,000 次模拟；120 人性能 |
| **P1 文本库** | §6 管线工具；**第一个故事＝100 条校准批**；按校准结果定配额；评测集；V-09、V-10 | 0.5 demo 完成后 | AC 4、8；实测单条审校分钟数 |
| P2 在线增强（可选，附录 B） | 不排期 | — | — |

每期停下都是可发布状态：P0a 停下＝引擎与工具就绪、游戏不变；P0b 停下＝0.4 内两次结算可玩（模板文本）；P0.5 停下＝第一篇章全程命运可玩（模板文本）；P1 停下＝有审校文本。

**P0 不做的事**（制作人意见 11）：物品流转／适性变化／赠物退回只留字段与引用校验（兵书沙盒后实装）；记忆压缩实装但只单元测试；形式化可达性证明不做。

---

## 11. 调参旋钮

| 旋钮 | 默认 | 安全范围 | 影响 | P0 开放 |
| --- | --- | --- | --- | --- |
| τ | 0.10 | 0.05–0.3 | 越低越确定 | 是 |
| H[T1] / H[T2] | 0.35 / 0.25 | 0.1–0.6 | 史实惯性 | H[T1] 是 |
| deviationBudgetRatio | 0.15 | 0.05–0.30 | 每阶段最多偏离比例 | 是 |
| randomDeviationRate | T1 0 / T2 0.03 / T3 0.08 | 0–0.15 | 每人每阶段抽中带因随机事件的概率 | 是 |
| wP / wA / wR / wI | 0.25 / 0.20 / 0.20 / 0.35 | 各 0–0.5 | 各项比重 | 冻结 |
| EPS | 0.05 | 0–0.1 | 随机扰动；**模拟器必须输出 EPS 对分布影响的报告项**（相对 τ 是半个指数单位，制作人意见 12） | 冻结 |
| relationCauseThreshold | 40（相对上升量） | 25–60 | 关系作为“因”的门槛 | 冻结 |
| memoryCap | 40 | 20–80 | 压缩阈值 | 冻结 |
| favorBand 边界 | 40 / −20 | ±10 | 文本档位 | 冻结 |
| leaveParty 阈值 | favor < 0 或 trust < 15 | — | 离队候选前置 | 冻结 |

---

## 12. 待用户决定的问题

| # | 问题 | 方案 | 推荐 |
| --- | --- | --- | --- |
| Q1 | **已决定（用户 2026-10-27 14:27 选“放刘备线第四篇章逸话”；按 2026-10-27 定稿的篇章划分，209 南郡中箭落在第三篇章「赤壁」lb3，卡片上的“第四篇章”是旧六幕编号，故落点＝lb3 逸话；第三篇章剧本 §6 Z13 原写“只编年、不给干预入口”，下次修订时加入该逸话）**。周瑜救人（210 巴丘病卒）放在哪个篇章（G-10）？史实：《周瑜传》南郡之战“流矢中右胁，疮甚”、210 年“道于巴丘病卒”，临终荐鲁肃，程普领南郡太守，其后孙权借南郡与刘备；“中曹仁之计箭疮致死”为演义第 51、56–57 回 | A 东吴线延续到赤壁后（需补写周瑜为主角的篇章）；B 放在刘备线「赤壁」篇章（lb3）作逸话：刘备驻公安、周瑜驻江陵的联盟期，条件由刘备方满足（原创医者能人，**不用华佗**，208 年已卒），存活形态为“疮重不能领兵、退养柴桑”，`cannotHoldPosts` 含南郡太守与领兵职，`mustNotBePresentAt` 含刘备入蜀各节点；C 暂不做 | **B**（叙事总监核验后可行） |
| Q2 | 孙策被救后的权位处理 | A 伤重退养、正式传位孙权（史料出发点：《孙讨逆传》裴注引《吴历》医者言“百日勿动”，孙策照镜发怒疮裂而死——救人条件即“出猎护卫＋许贡一线＋百日静养”；**不用演义第 29 回于吉情节**，PROJECT §4.7 不神怪）；B 孙策继续为主 | **A**（B 违反政权归属不变） |
| Q3 | 接受“偏离需因、因果同向”？意味着什么都不做时历史名将几乎不偏离（只有随机事件） | 接受／改回 v4 纯打分 | **接受**（叙事总监与技术总监均同意） |
| Q4 | 文本库配额 | 5,000 条为硬上限，**实际配额由 100 条校准批反推 40 小时预算**；全审不改抽检 | **按此执行** |
| Q5 | 刘备线篇章划分已随剧本定稿（§3.2）；曹操线、东吴线篇章划分未定 | 各线立项时定 | `cc*`、`sc*` 占位编号标 Provisional |

---

## 13. 评审记录（CCGS，2026-10-27）

| 门禁 | 评审角色 | 对 v5.0 的结论 | v5.1 处理 |
| --- | --- | --- | --- |
| TD-FEASIBILITY | technical-director | CONCERNS（F-01～F-13） | 已修正：F-01 定点索引溢出→`long` 中间值、索引公式与取整规则（§5.3）；F-02 随机流表、PCG/SplitMix/FNV 参数（§5.3）；F-03 令牌带标签且因果同向、RELATION 用相对上升、`seen` 迁移 importance 2、cause 取法（§4.3、§4.6、§5.2）；F-04 两步提交保存令牌、提交复核阈值不重抽、宣告期走默认、冷却（§5.2 ⑤）；F-05 整档事务（§7.2）；F-06 预写标记、从第①步重跑（§3.3、§7.2）；F-07 ChronicleRecord 按字段分主、默认终局、整份复制、迁移（§4.9、§7.3）；F-08 `alive` 谓词含 pendingDeath（§4.4）；F-09 跨篇章 V-01、powerTransfer atNode（§4.5、§8.1）；F-10 §5.1 全程 Q16.16、偏好书作用于 Δfavor；F-11 JSON 整数化（§4）；F-12 性能预算拆分（§8.3）；F-13 seqCounter、在场定义、地区表、默认候选无前置、脚本化加入走日志 |
| TD-ADR | technical-director | CONCERNS（A-01～A-07） | 已修正于 ADR-0013 v2：补 Performance Implications、Migration Plan、Validation Criteria、Related；风险改表；ADR-0001 依赖修订条款与 DTO 约定；`Settle` 签名加 `IConditionContext`、`IFateQuery`、`TextQuery`；整档事务接口；`File.Replace`；JSON 发行形态；Steam Deck 说明 |
| ND-CONSISTENCY | narrative-director | CONCERNS（A1–A6、B7–B11、C12–C13、D14–D16、E17–E21、F22–F24、G25–G26） | 已修正：赵云示例改为第一篇章只有默认候选＋N7/N8 `presentAt` 锚点、`activeFrom`、初始关系标签清空、锚点 id 改 panhe（§4.3）；在队人物候选范围规则（§4.3、V-04）；严纲改 `dies` 锚点、`mustKeep` 校验（§4.7、V-06）；算例八维全算（§5.1）；文本示例改正并列为黄金反例（§4.8）；救人结构加 `subject`、`unchangedDeaths`、对话类 `sceneVariant`、hint 文案规则（§4.5）；篇章编号标 Provisional（§3.2、Q5）；Q1/Q2 按建议改写并补史料（§12）；T3 死亡允许 HISTORY 因与剧情直写（§5.2）；REACT 四处修正（§5.1）；文本键加 `stageGroup`（§4.8、§5.7）；跨州事件 delay ≥1（§5.5）；lint 补六项与引用白名单（§6.3）；庞统／郭嘉／周瑜／孙策出处分开标注（见下表）。叙事总监结论：修正 A、B 两组后可复审为 APPROVE |
| PR-SCOPE | producer | OPTIMISTIC（12 条） | 已修正：P0 拆 P0a／P0b，新增 P0.5 第一篇章命运数据，P1 先做 100 条校准批（§10）；可达性证明降为模拟器报告（§8.2）；测试与校验分级（§8.1、§8.3）；AC 按期归属（§8.4）；迁移随 0.4 一次完成（§7.1）；文本量改为“待审条数含 variant”≈960–2,800（§6.2）；审校按 30 秒–3 分钟区间、配额反推（§6.6、Q4）；命运面板限定为 EditorWindow 表格（§9）；ChronicleRecord／物品流转移出 P0（§10）；旋钮 P0 只开放四个、EPS 列为模拟器报告项（§11）。制作人结论：按此修订可回到 REALISTIC |

**六个首批救人案例（叙事总监核验表，供各篇章立项时逐条过 ND-CONSISTENCY）**。另：剧本线程已为刘备线每篇各定一条救人条件——陈宫（lb2，白门楼，“奈卿老母何”为“本可如此”出发点，0.7 先做脚本版）、糜夫人（lb3，长坂，史实无其死）、庞统（lb4）——它们同样按 §4.5 结构建档；0.7 的“脚本版”救人用剧情标志实现，命运引擎版（P0b 之后）读取同一标志并入 `rescueResults`，不重做条件。

| 人物 | 篇章（占位编号） | 死亡节点年份 | 死因来源标注 | powerTransfer | affectedNodes 建议 | 结论 |
| --- | --- | --- | --- | --- | --- | --- |
| 关羽 | 刘备线 lb5「终幕」 | 219 麦城／临沮 | 【史实】《关羽传》；突围【原创 if】 | 不需要（`cannotHoldPosts` 荆州牧） | 张飞之死（221，动机不变）、称帝、夷陵（动机由报仇改夺荆州）、白帝托孤、尾声；`unchangedDeaths` 关平、赵累 | 可行 |
| 庞统 | 刘备线 lb4「入蜀」（第四篇章剧本 §10.1 已写条件：的卢不换＋不入城弩格＋涪城大宴回答） | 214 雒城 | 死因【史实】《庞统传》流矢；地点【演义】第 63 回落凤坡；【原创 if】 | 不需要 | 取成都、汉中之战、称汉中王封赏、夷陵谏止一句（避免“改史”暗示）、尾声 | 可行，最干净 |
| 郭嘉 | 曹操线 cc3 | 207 自柳城还、疾笃 | 【史实】《郭嘉传》病卒（归途）；留许养病【原创 if】 | 不需要 | 柳城（“兵贵神速”行前已献）、共同终章（“郭奉孝在，不使孤至此”改存活版）、立嗣 | 可行 |
| 典韦 | 曹操线 cc3 首节 | 197 宛城 | 【史实】《典韦传》《武帝纪》；援军【原创 if】 | 不需要 | 哭典韦改慰伤、`unchangedDeaths` 曹昂、曹安民、丁夫人决裂、官渡与终章在场 | 可行 |
| 周瑜 | 刘备线 lb3「赤壁」逸话（Q1，用户已定；节点 209 南郡） | 210 巴丘 | 【史实】《周瑜传》南郡中流矢、巴丘病卒；箭疮致死【演义】第 51／56–57 回；识破计谋【原创 if】 | 不需要传位；`cannotHoldPosts` 南郡太守与领兵职 | 借荆州、刘备入蜀（不得在场）、娶孙夫人、鲁肃接任、尾声变体；终章不受影响 | 有条件可行 |
| 孙策 | 东吴线 sc1 末节 | 200 丹徒出猎 | 【史实】《孙讨逆传》许贡客；《吴历》百日勿动【史实·裴注】；获救【原创 if】；于吉【演义】不用 | **需要**：`lord_jiangdong → sun_quan`，`retires_wounded`（Q2=A） | 临终托付改让位托付、张昭周瑜辅政、尾声变体、终章在场不领兵、荆州／合肥不在场 | 有条件可行（`subject=protagonist`） |

---

## 附录 A：v4 → v5.1 字段与规则映射

| v4 | v5.1 |
| --- | --- |
| `node_191` 阶段 id | `lb1.N5`（§3.2） |
| `playerImpression` | `relations[protagonistId]`（含 `views`、`baseline`） |
| `MemoryEntry` 无 key | 必有 `who`+`key`，同 0.2 |
| `fateHistory.decidedBy/reason` | `cause`（因果令牌） |
| 第⑤步 AI 裁决、`aiMargin` | 删除；定点 softmax 抽签＋两步提交 |
| 存档 AI 缓存、待提交日志 | 删除；整档原子事务 |
| `IFateResolver` 双实现 | 只有 `FateEngine`；在线版见附录 B |
| 6.5 单段 `conditions` | `eligibility` + `fulfillment` + `subject` + `unchangedDeaths` |
| 锚点 `removeAnchor` | `liftAnchor` + `tierAfter=T1S` + `survivorConstraints` + `powerTransfer` |
| 可达性“每个可招募人物” | `recruitableIn` 按篇章；模拟器可达性报告 |
| 11.3 运行成本表 | 删除；§6.6 开发期成本 |

## 附录 B：运行时在线模式（P2，不实装）

保留 v4 第 7、8、11、13.1 节的要点供日后立项：模型只在候选 id 中选、输出严格 JSON、占位符与黑名单、未来词表、输入哈希缓存、代理服务器与合规。若立项，另立 ADR，用 `IOnlineAdviser` 覆盖第④步“拟选”，覆盖结果必须通过第⑤–⑦步同样的校验。
