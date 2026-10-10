using System;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    /// <summary>参与调度的资源种类（数组下标用）。</summary>
    public enum ResourceKind
    {
        /// <summary>金钱</summary>
        Gold = 0,
        /// <summary>粮草</summary>
        Food = 1,
        /// <summary>兵力</summary>
        Troop = 2,
        /// <summary>兵装（枪 / 戟 / 弩 / 马）</summary>
        Arms = 3,
        /// <summary>器械（冲车 / 投石）—— 攻城器具，前线更需要</summary>
        Machine = 4,
        /// <summary>船（走舸 / 楼船 / 斗舰）—— 港关与水域作战更需要</summary>
        Boat = 5,
    }

    /// <summary>
    /// 一张调拨单：从一座城送一批资源到另一座城。
    /// 同一对（源城 → 目标城）的多类资源会**合并成一张单**，共用一个运输主将（人是稀缺资源）。
    /// </summary>
    public struct ResourceShipment
    {
        /// <summary>源城 id</summary>
        public int fromCityId;
        /// <summary>源城名（报告用）</summary>
        public string fromCityName;
        /// <summary>目标城 id</summary>
        public int toCityId;
        /// <summary>目标城名（报告用）</summary>
        public string toCityName;

        /// <summary>金钱</summary>
        public int gold;
        /// <summary>粮草</summary>
        public int food;
        /// <summary>兵力</summary>
        public int troops;
        /// <summary>兵装件数（枪 / 戟 / 弩 / 马）</summary>
        public int arms;
        /// <summary>器械件数（冲车 / 投石）</summary>
        public int machines;
        /// <summary>船只数</summary>
        public int boats;
        /// <summary>
        /// 本单为运兵预留的**护送粮**（已计入 <see cref="food"/>）：
        /// 随车带走、路上吃掉一部分、剩下的随军进城当缓冲。
        /// 用途：① 报告里标出"这批兵带了口粮"；② 执行层按它兜底缩兵（源城粮变少时按比例少运兵）。
        /// </summary>
        public int escortFood;

        /// <summary>
        /// 本单为运兵预留的**随行兵装**（已计入 <see cref="arms"/>）。
        /// 兵没有兵装就拉不出一支部队（组建要扣 <c>TroopType.costItems</c>），
        /// 所以运兵必须配装 —— 否则进城的是几万张白吃粮的嘴。
        /// </summary>
        public int escortArms;

        /// <summary>
        /// 本条发货的**行程跳数**（源城 → 实际收货城；中转接力时是到中转城那一腿的估算值）。
        /// 执行层据此算"车队自备口粮"：路上会吃掉的（跳数 × 每跳回合）**外加** 10 天口粮。
        /// </summary>
        public int hops;

        /// <summary>实际收货城的圈层（中转接力时是**中转城**的圈层，不是最终目标的）</summary>
        public int toRing;
        /// <summary>
        /// 这张单服务的**最终目标城**的优先级档
        /// （含"紧急插队"修正：缺口过大的城按上一个圈层对待）。执行顺序的主序。
        /// </summary>
        public int toTier;
        /// <summary>最终目标城的缺口紧急度（各类资源缺失比例之和 × 100）：同档内先发最急的</summary>
        public int toUrgency;
        /// <summary>调度理由（"前线防御 水位90%"之类，报告用）</summary>
        public string reason;

        /// <summary>是否已派出（执行模式下的统计，报告用）</summary>
        public bool executed;
        /// <summary>未派出的原因（executed 为 false 时非空）</summary>
        public string failReason;

        /// <summary>本单的总货量（排序用）</summary>
        public int TotalAmount()
        {
            return gold + food + troops + arms + machines + boats;
        }

        /// <summary>是否为空单</summary>
        public bool IsEmpty()
        {
            return gold <= 0 && food <= 0 && troops <= 0 && arms <= 0 && machines <= 0 && boats <= 0;
        }
    }

    /// <summary>
    /// 一条"运不出去"的记录：某城某类资源有富余（超出自身水位），但本趟分配里找不到接收方
    /// （前线也满 / 没有可达的缺口）。
    ///
    /// 用途：给产物端一条**背压**信号 —— 容器满了以后继续生产的前提是"运得走"，
    /// 一旦运不出去就该停（见 <see cref="ResourceDispatchState.CanKeepProducing"/>）。
    /// </summary>
    public struct ResourceBacklog
    {
        /// <summary>城池 id</summary>
        public int cityId;
        /// <summary>城池名（报告用）</summary>
        public string cityName;
        /// <summary>资源种类（<see cref="ResourceKind"/> 的整数值）</summary>
        public int kind;
        /// <summary>运不出去的富余量</summary>
        public int amount;
    }

    /// <summary>
    /// 一条"兵装待产"诊断：某城守军的兵装缺口 + 生产端的当前阻塞原因。
    ///
    /// 为什么要诊断：兵装为 0 通常不是"没安排生产"，而是**生产命令跑不起来** ——
    /// <c>CityAI.AICreateItems</c> 开头有一串前置（空闲武将 / 空闲锻冶或马厩 / 金钱 / 各类目标），
    /// 任何一条不满足都会静默 return。报告里把这条原因写出来，才不会只知道"缺兵装"却查不到为什么。
    /// </summary>
    public struct ArmsDemandNote
    {
        /// <summary>城池 id</summary>
        public int cityId;
        /// <summary>城池名</summary>
        public string cityName;
        /// <summary>本城守军还差多少件兵装（兵力 × 覆盖比例 − 现有兵装）</summary>
        public int gap;
        /// <summary>生产端的阻塞原因（空串 = 生产条件齐备，下回合就能造）</summary>
        public string blocker;
    }

    /// <summary>
    /// 一次资源调度求解的完整结果。影子 / 仅参考模式下只产出本对象，不派任何运输队。
    /// </summary>
    public class ResourcePlan
    {
        /// <summary>势力 id</summary>
        public int forceId;
        /// <summary>势力名</summary>
        public string forceName;
        /// <summary>军团作用域 id（0 = 势力级作用域，无军团边界）</summary>
        public int corpsId;
        /// <summary>军团标签（势力级作用域为 null）</summary>
        public string corpsName;
        /// <summary>是否"只算不运"（影子模式 / 玩家直辖军团）</summary>
        public bool adviceOnly;

        /// <summary>调拨单（按执行顺序：目标圈层升序）</summary>
        public List<ResourceShipment> shipments = new List<ResourceShipment>();
        /// <summary>各城资源水位快照（报告用）</summary>
        public List<string> cityState = new List<string>();
        /// <summary>本趟**实际纳入调度**的城池（已剔除"运输禁止"的军团等），供发布覆盖信号用</summary>
        public List<City> cities = new List<City>();
        /// <summary>未满足的缺口 / 未执行的说明</summary>
        public List<string> unmet = new List<string>();
        /// <summary>运不出去的富余（产物端的背压信号）</summary>
        public List<ResourceBacklog> backlog = new List<ResourceBacklog>();
        /// <summary>
        /// 兵装待产诊断：本城守军还差多少兵装、以及**现在为什么造不出来**
        /// （没有空闲武将 / 没空闲锻冶马厩 / 金钱不足 / 各类已达城内目标）。
        ///
        /// 注意：本列表只用于报告与"抽运输主将时给生产留人"这两件事；
        /// 城池 AI 命令的优先级调整不读它 —— 那边直接按 CitySituation 现算（避免两套口径）。
        /// </summary>
        public List<ArmsDemandNote> armsDemand = new List<ArmsDemandNote>();
        /// <summary>本回合实际派出的车数（执行模式）</summary>
        public int executedCount;

        /// <summary>
        /// 前线紧急度（0~1）：0 = 从容（后方只被抽到水位线的 20%），1 = 告急（后方允许被抽空）。
        /// 只用于报告与调参观察，实际扣减在 ResourceBalance.SourceAvail。
        /// </summary>
        public float frontUrgency;

        /// <summary>计划派车数</summary>
        public int PlannedCount()
        {
            return shipments != null ? shipments.Count : 0;
        }

        /// <summary>
        /// 生成人类可读报告（供调参与对拍）。
        /// </summary>
        /// <param name="title">首行标题前缀；为空时用中性的"[资源调度]"</param>
        /// <returns>报告文本</returns>
        public string Report(string title = null)
        {
            StringBuilder sb = new StringBuilder();
            if (string.IsNullOrEmpty(title))
                title = "[资源调度]";
            sb.Append(title).Append(" | ");

            sb.Append('#').Append(forceId).Append(' ').Append(forceName);
            if (!string.IsNullOrEmpty(corpsName))
            {
                sb.Append(" · ").Append(corpsName);
                if (adviceOnly)
                    sb.Append("(仅参考)");
            }
            sb.Append(" 城池:").Append(cityState != null ? cityState.Count : 0)
              .Append(" 计划派车:").Append(PlannedCount())
              .Append(" 实际派出:").Append(executedCount)
              .Append(" 运不走:").Append(backlog != null ? backlog.Count : 0)
              // 城况行的读法：每类资源是"现有/水位线 达成率"（水位线是绝对数量，见 resourceDispatch.water*）
              .Append(" | 城况: 现有/水位线(达成率)");
            if (frontUrgency > 0f)
                sb.Append(" 前线紧急度:").Append(frontUrgency.ToString("P0"));
            sb.AppendLine();

            if (cityState != null)
            {
                for (int i = 0; i < cityState.Count; i++)
                    sb.Append("   ").Append(cityState[i]).AppendLine();
            }

            if (shipments != null)
            {
                for (int i = 0; i < shipments.Count; i++)
                {
                    ResourceShipment s = shipments[i];
                    sb.Append("   → ").Append(s.fromCityName).Append(" ⇒ ").Append(s.toCityName)
                      .Append(" 金").Append(s.gold)
                      .Append(" 粮").Append(s.food);
                    // 运兵的单：标出这批兵随车带了多少口粮（粮不够会按粮缩兵，见 ResourceBalance）
                    if (s.escortFood > 0)
                        sb.Append("(随军粮").Append(s.escortFood).Append(')');
                    sb.Append(" 兵").Append(s.troops)
                      .Append(" 装").Append(s.arms);
                    if (s.escortArms > 0)
                        sb.Append("(随军装").Append(s.escortArms).Append(')');
                    sb.Append(" 器").Append(s.machines)
                      .Append(" 船").Append(s.boats)
                      .Append(" | ").Append(s.reason);
                    if (s.executed)
                        sb.Append("  [已派出]");
                    else if (!string.IsNullOrEmpty(s.failReason))
                        sb.Append("  [未派出: ").Append(s.failReason).Append(']');
                    sb.AppendLine();
                }
            }

            if (unmet != null)
            {
                for (int i = 0; i < unmet.Count; i++)
                    sb.Append("   ! ").Append(unmet[i]).AppendLine();
            }

            // 运不出去的富余（背压）：产物端据此决定"满仓后要不要继续生产"
            if (backlog != null && backlog.Count > 0)
            {
                sb.Append("   运不走(背压): ");
                for (int i = 0; i < backlog.Count; i++)
                {
                    if (i > 0)
                        sb.Append(' ');
                    sb.Append(backlog[i].cityName).Append('·').Append(KindName(backlog[i].kind))
                      .Append('x').Append(backlog[i].amount);
                }
                sb.AppendLine();
            }

            // 兵装待产：缺兵装的城 + 生产端当前阻塞在哪一步（"为什么兵装一直是 0"的答案）
            if (armsDemand != null && armsDemand.Count > 0)
            {
                sb.Append("   兵装待产: ");
                for (int i = 0; i < armsDemand.Count; i++)
                {
                    if (i > 0)
                        sb.Append(' ');
                    sb.Append(armsDemand[i].cityName).Append("缺").Append(armsDemand[i].gap);
                    if (!string.IsNullOrEmpty(armsDemand[i].blocker))
                        sb.Append('(').Append(armsDemand[i].blocker).Append(')');
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>资源种类中文名（报告用）。</summary>
        /// <param name="kind"><see cref="ResourceKind"/> 的整数值</param>
        /// <returns>单字中文名</returns>
        public static string KindName(int kind)
        {
            switch ((ResourceKind)kind)
            {
                case ResourceKind.Gold: return "金";
                case ResourceKind.Food: return "粮";
                case ResourceKind.Troop: return "兵";
                case ResourceKind.Arms: return "装";
                case ResourceKind.Machine: return "器";
                default: return "船";
            }
        }
    }
}
