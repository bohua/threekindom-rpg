# Epic：战斗打击感（hit-feel）

- 设计：共享目录 `design/hit-feel/HIT_FEEL_DESIGN.md` v0.2（CCGS 三方评审有条件通过，必改项已并入；用户 2026-10-30“按建议走”）
- 范围：本次 P0＋P1。P2（飘字六档、原因标签、气势飞入、粒子、刀光、箭矢、冲击波，需 Codex 设计图）、P3（音效，CC0）、P4（单挑与破将演出、战功牌）另做。
- 铁律：不改伤害、命中、AT、气势、策力的数值或规则，不加暴击；`Combat.Damage`、`Floored`、`Hit` 签名不动；顿帧和慢放不推进 AT。
- 代码：`Assets/Prototypes/BingshuTactics/Runtime/`（`HitFeel.cs`、`HitFeelProfile.cs`、`TacticsGame.HitFeel.cs`，以及 `TacticsGame.cs`／`Visuals`／`Camera`／`Duel` 的接线）

| 故事 | 内容 | 状态 |
| --- | --- | --- |
| [story-001](story-001-p0-foundation.md) | P0 打底：随机源隔离、事件层、档位解析、强度表、峰值槽、表现时钟、调试开关、开关对比测试与档位统计 | 完成 |
| [story-002](story-002-p1-feedback.md) | P1 纯程序反馈：闪白插值到白并分档、击退、挤压拉伸、顿帧、方向性震屏、破将推近、转身溃退、设置项 | 完成（设置项的菜单界面待设计图） |
