# Story 001：打击感 P0 打底

- Epic：[hit-feel](EPIC.md) · 设计稿第 2、3、6 节 · 类型：Logic＋Integration

## 验收标准

1. 战斗随机（敌军放策略的六成判定、单挑吕布出招）改用带种子的独立随机源（`BattleRng`，种子＝`Chapter.Seed`，测试可用 `TacticsGame.SeedOverride` 注入）；表现随机（呼吸、火焰相位、溃退抖动）用另一个 `System.Random`。战棋代码里不再读 `UnityEngine.Random`。
2. `Hit()` 签名不变；其中另调 `Combat.AdvMul`、`Combat.HeightMul` 等组装 `HitEvent`（来源、正负原因、代受／防御、下限截断、击破、具名／首领、最后一击、方向）。下限截断用 `Floored` 前后差值判断。
3. `HitFeel.Resolve(HitEvent)` 为纯函数，按第 2 节返回 钝／普通／重击／击破／破将／单挑（含决胜），EditMode 覆盖各档与“取最高一档”。
4. `HitFeelProfile`（ScriptableObject）装第 3 节强度表：时长毫秒、震屏 1080p 像素、形变源像素；放 `Resources/BingshuTactics/HitFeelProfile` 即可调参，无资产时用代码初值。
5. 行动级峰值槽：`BattleLoop` 每个单位行动开头开槽、结尾关槽；`Hit` 只登记，`LateUpdate` 取本帧最高档触发顿帧／震屏／推近，同一行动只在出现更高档时再触发；单挑每回合交锋单独开槽。
6. 顿帧改为剩余时长倒计时（至少一帧），各表现系统与动作播放统一读 `fxDt`，不用 `timeScale = 0`；按确认键可跳过剩余停顿。
7. 开发版分层调试开关：F1 总开关（关＝改动前旧反馈），F2–F8 逐层开关各招，左上角显示 4 秒。
8. 对比测试：PlayMode 电脑代打同一种子，打击感开／关各跑一次，逐行比对 TACTICS 日志与每次命中；另跑 100 场以上输出各档比例（目标重击 ≤ 约 30%）。

## 测试

- EditMode：`Assets/Prototypes/BingshuTactics/Tests/HitFeelTests.cs`
- PlayMode：`Assets/Tests/PlayMode/HitFeelComparisonTests.cs`（统计需环境变量 `HITFEEL_STATS=<报告路径>`）
