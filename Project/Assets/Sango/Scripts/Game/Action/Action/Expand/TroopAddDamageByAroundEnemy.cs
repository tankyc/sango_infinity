using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按周围敌军数量附加伤害与回气（汉土特技 0.4.22 —— 30137 长驱策）。
    ///
    /// <para>
    /// 【设计原文】攻击敌军时，回复 2 格范围内敌部队数 ×5 的气力，
    /// 并额外造成敌部队数 ×50 的伤害。
    /// </para>
    /// <para>
    /// 【为什么不做成条件】现有 <c>TroopAroundEnemyCount</c> 只返回布尔（够 / 不够），
    /// 拿不到"几支"这个数值；而把它改成"带半径的数值查询"会进条件热路径
    /// （见该类注释明确反对放开半径）。这里按"伤害结算时算一次"实现：
    /// 每次掉兵事件只有一支攻击方会通过过滤，<c>Spiral(radius)</c> 只在有效回调里跑。
    /// </para>
    /// <para>
    /// 【口径】
    /// · 只在"我方是攻击方"且非反击、非计略时生效（与乘险同口径）；
    /// · 统计范围 = 本队所在格 <c>Spiral(radius)</c>（默认 2 格，含中心格），
    ///   只数 <c>IsEnemy</c> 的部队并排除自己；
    /// · 伤害按固定值扣兵（<c>N × damagePerEnemy</c>），不参与百分比上限；
    /// · 回气 <c>N × moralePerEnemy</c>，走 <c>Troop.ChangeMorale</c>（可被其他特技修正）。
    /// </para>
    /// <para>
    /// 【去重键 = 出手序号】<see cref="SkillInstance.GetCastSerial"/>。
    /// 同一发战法在时间轴上会多次进入 <c>Action</c>（多段），范围战法还会在一次
    /// <c>Action</c> 里逐个目标结算（多目标）；若按调用次数结算，就会变成
    /// "多段 = 多次附加伤害 + 多次回气"。序号由出手入口换发
    /// （<c>Troop.SpellSkill</c>、连击 <c>TroopComboAttack</c>、战法失败后的普攻替打
    /// <c>TroopSpellSkillFailEvent</c>），所以连击 / 反射 / 下一次出手都会拿到新号。
    /// </para>
    /// <para>
    /// 【作用目标数：三态】<c>multiTarget</c> / <c>moraleMultiTarget</c> 共用同一套取值：
    /// · <c>0</c>（缺省）：一次出手只认**首个**真正结算到的目标；
    /// · <c>1</c>：**每个**被命中的目标各结算一次（同一目标的多段仍然折叠）；
    /// · <c>2</c>：只认**主目标格**——即施法时选中的那一格（<see cref="SkillInstance.castSpellCell"/>），
    ///   不依赖"循环里谁先结算"这个顺序假设。主目标格上没部队时本类不生效。
    /// </para>
    /// <para>
    /// 【伤害与回气可以不同口径】<c>moraleMultiTarget</c> 缺键时**跟随** <c>multiTarget</c>
    /// （不配置 = 两者同口径）；显式配置后独立生效，典型用法：
    /// <c>multiTarget = 1</c> + <c>moraleMultiTarget = 0</c> = 每个目标都吃附加伤害，但气力整发只回一次。
    /// </para>
    /// JSON 参数：<c>radius</c>（默认 2）、<c>damagePerEnemy</c>（缺省 50，显式 0 = 关闭附加伤害）、
    /// <c>moralePerEnemy</c>（缺省 5，显式 0 = 关闭回气）、<c>multiTarget</c>（缺省 0）、
    /// <c>moraleMultiTarget</c>（缺省跟随 multiTarget）。
    /// </summary>
    public class TroopAddDamageByAroundEnemy : TroopTroopActionBase
    {
        /// <summary>统计半径（默认 2 格）</summary>
        int radius = 2;
        /// <summary>每支敌军附加的固定伤害（默认 50）</summary>
        int damagePerEnemy = 50;
        /// <summary>每支敌军回复的气力（默认 5）</summary>
        int moralePerEnemy = 5;
        /// <summary>伤害的作用目标数：0 首个结算目标（缺省）/ 1 每个目标 / 2 仅主目标格</summary>
        int multiTarget;
        /// <summary>回气的作用目标数：取值同上；缺键时在 Init 里跟随 <see cref="multiTarget"/></summary>
        int moraleMultiTarget;

        /// <summary>统计过程中的临时计数（避免每次结算新建容器）</summary>
        int scanCount;
        /// <summary>Spiral 回调（缓存，避免每次结算新建委托）</summary>
        System.Action<Cell> scanAction;

        /// <summary>
        /// 本发出手已经加过伤害的目标（复用容器，避免每次出手新建）。
        /// 口径 0/2 下最多 1 个；口径 1 下按命中目标数增长，同一目标的多段靠它折叠。
        /// </summary>
        readonly List<Troop> appliedDamageTargets = new List<Troop>(8);

        /// <summary>本发出手已经回过气的目标（与伤害表分开，两者口径可以不同）</summary>
        readonly List<Troop> appliedMoraleTargets = new List<Troop>(8);

        /// <summary>两张记账表当前对应的出手序号（0 = 还没结算过任何出手）</summary>
        int appliedSerial;

        /// <summary>初始化并订阅兵力变化事件</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">宿主对象（[0] 必须是部队）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            int r = p.Value<int>("radius");
            radius = r > 0 ? r : 2;

            // 缺键与"显式 0"要区分：缺键给默认值，显式 0 表示关闭该项
            JToken dmgToken = p["damagePerEnemy"];
            damagePerEnemy = dmgToken != null ? dmgToken.ToObject<int>() : 50;

            JToken moraleToken = p["moralePerEnemy"];
            moralePerEnemy = moraleToken != null ? moraleToken.ToObject<int>() : 5;

            // 伤害口径：缺键 = 0（首个结算目标）
            multiTarget = ReadTargetMode(p["multiTarget"], 0, "multiTarget");

            // 回气口径：缺键 = 跟随伤害口径（不配置时两者一致，与改造前行为相同）
            JToken moraleModeToken = p["moraleMultiTarget"];
            moraleMultiTarget = moraleModeToken != null
                ? ReadTargetMode(moraleModeToken, multiTarget, "moraleMultiTarget")
                : multiTarget;

            // 先退订：下面任何提前 return 都不能把上一次的订阅留在全局静态事件上
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
            appliedDamageTargets.Clear();
            appliedMoraleTargets.Clear();
            appliedSerial = 0;

            if (Troop == null)
            {
                Sango.Log.Error("TroopAddDamageByAroundEnemy 必须装配在部队上（需要读取本队所在格与气力），该配置不会生效");
                return;
            }

            // 本类只作用于攻击方：isDefender = 1 会被"施加方必须是本队"那道快速拒绝挡掉，
            // 配错时表现为"特技完全不触发"，这里提前喊一声，免得去翻战斗日志。
            if (isDefender != 0)
                Sango.Log.Warning("TroopAddDamageByAroundEnemy 只作用于攻击方（isDefender 必须为 0），当前配置的 isDefender 非 0，该配置不会生效");

            // 两项都关：订阅了也是空转，直接不订阅，省掉全局事件上的每次回调
            if (damagePerEnemy == 0 && moralePerEnemy <= 0)
            {
                Sango.Log.Warning("TroopAddDamageByAroundEnemy 的 damagePerEnemy 与 moralePerEnemy 都是 0，不会产生任何效果，已跳过事件订阅");
                return;
            }

            if (scanAction == null)
                scanAction = ScanCell;

            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        /// <summary>退订事件并清掉本发记账（实例可能被复用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
            appliedDamageTargets.Clear();
            appliedMoraleTargets.Clear();
            appliedSerial = 0;
        }

        /// <summary>
        /// 兵力变化前按周围敌军数量附加伤害并回复气力。
        /// </summary>
        /// <param name="defencer">兵力变化的一方（被攻击目标）</param>
        /// <param name="atker">施加方</param>
        /// <param name="atkBack">反击信息（0 = 非反击）</param>
        /// <param name="overrideData">待修正的兵力变化量（负数为掉兵）</param>
        void OnTroopChangeTroops(Troop defencer, SangoObject atker, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (overrideData == null || overrideData.Value >= 0) return;
            if (IsAttackBack(atkBack)) return;

            // 【快速拒绝】OnTroopChangeTroops 是全局静态事件：战场上**每一支**装配了本特技的部队
            // 都会收到别人的掉兵事件。不先按"施加方是不是本队"挡一道，就得为每一次掉兵跑一遍
            // 完整的 CheckTroop（内含 kinds 数组扫描与 condition 的装箱比较）。
            // CheckTroop 内部本来也会用 master 判这一条，这里只是把它提到最便宜的位置：
            // AsTroop 是纯类型判断，不分配。
            Troop self = this.Troop;
            if (self == null || self.cell == null) return;
            Troop attacker = atker != null ? atker.AsTroop() : null;
            if (!ReferenceEquals(attacker, self)) return;

            if (!CheckTroop(defencer, atker)) return;

            // 计略不是"攻击敌军"的物理手段，且计略通常没有逐目标伤害结算
            SkillInstance skill = atker as SkillInstance;
            if (skill == null || skill.IsStrategy()) return;

            // 序号变了说明是新的一次出手，上一发的两张记账表一起作废
            int castSerial = skill.GetCastSerial();
            if (castSerial != appliedSerial)
            {
                appliedSerial = castSerial;
                appliedDamageTargets.Clear();
                appliedMoraleTargets.Clear();
            }

            // 伤害与回气各自按自己的口径判断"这一发还要不要给这个目标结算"
            bool wantDamage = damagePerEnemy != 0;
            bool wantMorale = moralePerEnemy > 0;
            bool needDamage = wantDamage && IsPending(appliedDamageTargets, multiTarget, defencer, skill);
            bool needMorale = wantMorale && IsPending(appliedMoraleTargets, moraleMultiTarget, defencer, skill);
            if (!needDamage && !needMorale) return;

            scanCount = 0;
            self.cell.Spiral(radius, scanAction);
            if (scanCount <= 0) return;

            // 统计到东西才记账：周围没敌军时不该把这次出手的额度"用掉"
            if (needDamage)
            {
                int extra = scanCount * damagePerEnemy;
                if (extra > 0)
                    overrideData.Value -= extra;
                appliedDamageTargets.Add(defencer);
            }

            // 回复气力（设计原文：敌部队数 ×5）
            if (needMorale)
            {
                int morale = scanCount * moralePerEnemy;
                if (morale > 0)
                    self.ChangeMorale(morale);
                appliedMoraleTargets.Add(defencer);
            }
        }

        /// <summary>Spiral 回调：累计周围敌军数量</summary>
        /// <param name="cell">当前格</param>
        void ScanCell(Cell cell)
        {
            if (cell == null) return;
            Troop t = cell.troop;
            if (t == null) return;

            Troop self = this.Troop;
            if (self == null || ReferenceEquals(t, self)) return;
            if (!t.IsEnemy(self)) return;

            scanCount++;
        }

        /// <summary>
        /// 解析"作用目标数"参数：0 / 1 / 2，越界回退 0 并告警。
        /// </summary>
        /// <param name="token">JSON 字段（可为 null）</param>
        /// <param name="defaultValue">缺键时的默认值</param>
        /// <param name="key">字段名（报错用）</param>
        /// <returns>规范化的口径值</returns>
        int ReadTargetMode(JToken token, int defaultValue, string key)
        {
            if (token == null) return defaultValue;

            int mode = token.ToObject<int>();
            if (mode < 0 || mode > 2)
            {
                Sango.Log.Warning($"TroopAddDamageByAroundEnemy 的 {key}={mode} 非法（0 首个结算目标 / 1 每个目标 / 2 仅主目标格），已按 0 处理");
                return 0;
            }
            return mode;
        }

        /// <summary>
        /// 按口径判断"这个目标这一次还需要结算吗"。
        /// </summary>
        /// <param name="applied">对应项目的本发记账表</param>
        /// <param name="mode">口径：0 首个结算目标 / 1 每个目标 / 2 仅主目标格</param>
        /// <param name="target">本次结算到的目标</param>
        /// <param name="skill">本次出手的技能实例（提供主目标格）</param>
        /// <returns>true = 仍需结算</returns>
        static bool IsPending(List<Troop> applied, int mode, Troop target, SkillInstance skill)
        {
            // 每个目标各一次：只挡同一目标的多段 / 重复结算
            if (mode == 1)
                return !applied.Contains(target);

            // 0 / 2 都是"一次出手只结算一次"，已经结算过就直接跳过
            if (applied.Count > 0)
                return false;

            // 2 = 仅主目标格：目标必须站在施法时选中的那一格上。
            // 主目标格拿不到（老路径没记录）时保守返回 false，避免误判成"全场首个目标"。
            if (mode == 2)
                return skill.castSpellCell != null && ReferenceEquals(target.cell, skill.castSpellCell);

            return true;
        }

        /// <summary>执行（本类只做伤害修正，不参与触发器链）</summary>
        /// <param name="trigger">触发器</param>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
