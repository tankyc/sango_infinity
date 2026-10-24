/*
 * 文件名：OutpostState.cs
 * 描述：据点（城市 / 港口 / 关隘）态势与 AI 命令队列的数据结构。
 *
 * 对齐出处：Open311 原版（D:\open311\s11）
 *   · 据点状态枚举   —— s11_enum_scen.h:60-65 的据点状态枚举
 *   · 据点命令枚举   —— s11_game_ai.h:7-38 的命令编号枚举
 *   · 据点态势结构   —— s11_game_ai.h:469-495 的据点态势结构
 *   · 运行时缓存     —— s11_game_ai.h:543-562 的 AI 上下文成员变量
 *
 * 设计说明：
 *   原版 AI 上下文是全局单例，据点态势数组 / action_point_ / idle_person_list_ 等都是它的成员，
 *   同一时刻只服务一个势力、一个军团、一个据点。本项目没有对应的上下文单例类，
 *   因此用静态容器 OutpostRuntime 承载同一份"回合级缓存"——
 *   语义等价（同样是一次只服务一个据点），只是宿主从单例搬到了静态类。
 *
 *   注意：这些数据**不进存档**。原版据点态势也是每回合由势力级装载函数重算的临时数据；
 *   唯一需要跨回合保留的只有"据点守将"（原版存在 AI 上下文的 guard 字段里），
 *   那份数据挂在 Force 上（见 Force.guardPersonIds）。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 据点状态。对齐 s11_enum_scen.h:60-65。
    /// <para>原版注释译文：Rear=后方（距国境≥2）/ Middle=边界（距国境1）/ Front=前线（国境）/ Battle=战斗（区域内有敌军）</para>
    /// </summary>
    public enum OutpostState
    {
        /// <summary>后方（距国境 2 以上）—— s11_enum_scen.h:60</summary>
        Rear = 0,
        /// <summary>边界（距国境 1）—— s11_enum_scen.h:61</summary>
        Middle = 1,
        /// <summary>前线（国境）—— s11_enum_scen.h:62</summary>
        Front = 2,
        /// <summary>战斗（区域内有敌军）—— s11_enum_scen.h:63</summary>
        Battle = 3,
        /// <summary>状态总数 —— s11_enum_scen.h:64</summary>
        Max = 4,
    }

    /// <summary>
    /// 据点 AI 命令编号。对齐 s11_game_ai.h:7-38 的 AIOrderIdEnum（数值必须与之一致，
    /// 因为命令队列表 update_outposts_data 是按原版数值语义排列的）。
    /// </summary>
    public enum AIOrderId
    {
        None = 0,
        /// <summary>内政开发（建设内政设施）—— s11_game_ai.h:10 AIOrderId_Develop</summary>
        Develop = 1,
        /// <summary>征兵 —— s11_game_ai.h:11 AIOrderId_Recruit</summary>
        Recruit = 2,
        /// <summary>生产兵装 —— s11_game_ai.h:12 AIOrderId_Produce</summary>
        Produce = 3,
        /// <summary>治安巡查 —— s11_game_ai.h:13 AIOrderId_Patrol</summary>
        Patrol = 4,
        /// <summary>商人（金/粮赤字时买卖）—— s11_game_ai.h:14 AIOrderId_Merchant</summary>
        Merchant = 5,
        /// <summary>训练 —— s11_game_ai.h:15 AIOrderId_Drill</summary>
        Drill = 6,
        /// <summary>防御出阵 —— s11_game_ai.h:16 AIOrderId_Defense</summary>
        Defense = 7,
        /// <summary>攻击出阵 —— s11_game_ai.h:17 AIOrderId_Attack</summary>
        Attack = 8,
        /// <summary>建设军事设施 —— s11_game_ai.h:18 AIOrderId_Build</summary>
        Build = 9,
        /// <summary>支援（向缺兵据点输送）—— s11_game_ai.h:19 AIOrderId_Support</summary>
        Support = 0xa,
        /// <summary>运输 —— s11_game_ai.h:20 AIOrderId_Transport</summary>
        Transport = 0xb,
        /// <summary>武将移动 —— s11_game_ai.h:21 AIOrderId_Move</summary>
        Move = 0xc,
        /// <summary>召唤 —— s11_game_ai.h:22 AIOrderId_Summon</summary>
        Summon = 0xd,
        /// <summary>人才探索 —— s11_game_ai.h:23 AIOrderId_Search</summary>
        Search = 0xe,
        /// <summary>登用 —— s11_game_ai.h:24 AIOrderId_Employ</summary>
        Employ = 0xf,
        /// <summary>赏赐 —— s11_game_ai.h:25 AIOrderId_Bonus</summary>
        Bonus = 0x10,
        /// <summary>离间（二虎竞食）—— s11_game_ai.h:26 AIOrderId_Disrupt</summary>
        Disrupt = 0x11,
        /// <summary>协助（驱虎吞狼）—— s11_game_ai.h:27 AIOrderId_Collaborate</summary>
        Collaborate = 0x12,
        /// <summary>流言 —— s11_game_ai.h:28 AIOrderId_Rumor</summary>
        Rumor = 0x13,
        /// <summary>登用敌方武将 —— s11_game_ai.h:29 AIOrderId_EmployEnemy</summary>
        EmployEnemy = 0x14,
        /// <summary>拆除设施 —— s11_game_ai.h:30 AIOrderId_Destroy</summary>
        Destroy = 0x15,
        /// <summary>计略（含离间/协助/流言）—— s11_game_ai.h:31 AIOrderId_Strategy</summary>
        Strategy = 0x16,
        /// <summary>探索（仅在必定成功时执行，保底命令）—— s11_game_ai.h:32 AIOrderId_SearchMax</summary>
        SearchMax = 0x17,
        /// <summary>登用（保底命令）—— s11_game_ai.h:33 AIOrderId_EmployMax</summary>
        EmployMax = 0x18,
        /// <summary>登用敌方（保底命令）—— s11_game_ai.h:34 AIOrderId_EmployEnemyMax</summary>
        EmployEnemyMax = 0x19,
        /// <summary>商人（资源过剩时卖出）—— s11_game_ai.h:35 AIOrderId_MerchantExtra</summary>
        MerchantExtra = 0x1a,
        /// <summary>吸收合并 —— s11_game_ai.h:36 AIOrderId_Merge</summary>
        Merge = 0x1b,
        /// <summary>命令总数 —— s11_game_ai.h:37 AIOrderId_Max</summary>
        Max = 0x1c,
    }

    /// <summary>
    /// 单个据点的态势数据。逐字段对齐 s11_game_ai.h:469-495 的据点态势结构。
    /// <para>本类只在回合内有效，每回合由 ForceAI.LoadForceOutpostData 重新计算。</para>
    /// </summary>
    public class OutpostInfo
    {
        /// <summary>3 格（关隘 2 格）内存在敌方部队 —— s11_game_ai.h:471 exist_enemy3</summary>
        public bool existEnemy3;
        /// <summary>5 格（关隘 4 格）内存在敌方部队 —— s11_game_ai.h:472 exist_enemy5</summary>
        public bool existEnemy5;
        /// <summary>资源不足（前线据点金/粮/兵力任一在储备线以下）—— s11_game_ai.h:473 shortage</summary>
        public bool shortage;
        /// <summary>本方部队数量 —— s11_game_ai.h:474 own_unit_count</summary>
        public int ownUnitCount;
        /// <summary>敌方部队数量 —— s11_game_ai.h:475 enemy_unit_count</summary>
        public int enemyUnitCount;
        /// <summary>邻接据点区域内的敌方部队数量 —— s11_game_ai.h:476 neighbor_enemy_unit_count</summary>
        public int neighborEnemyUnitCount;
        /// <summary>玩家部队数量 —— s11_game_ai.h:477 player_unit_count</summary>
        public int playerUnitCount;
        /// <summary>可到达的邻接敌方（含空城）据点数量 —— s11_game_ai.h:478 neighbor_enemy_outposts_count</summary>
        public int neighborEnemyOutpostCount;
        /// <summary>距国境的距离（无边境时保持 int.MaxValue）—— s11_game_ai.h:479 distance_from_border</summary>
        public int distanceFromBorder = int.MaxValue;
        /// <summary>本据点区域内的敌方军事设施数量 —— s11_game_ai.h:480 enemy_facility_count</summary>
        public int enemyFacilityCount;
        /// <summary>本据点所属武将数量（不含俘虏）—— s11_game_ai.h:481 person_count</summary>
        public int personCount;
        /// <summary>本回合可下令的武将数量 —— s11_game_ai.h:482 idle_person_count</summary>
        public int idlePersonCount;
        /// <summary>
        /// 本阵（敌方/无主的阵、砦）所在设施 Id；&gt;= 0 表示本据点被立了本阵。
        /// 对齐 s11_game_ai.h:483（原版该字段为"本阵设施 Id"）。
        /// </summary>
        public int mainCamp = -1;
        /// <summary>金钱收入 —— s11_game_ai.h:484 gold_revenue</summary>
        public int goldRevenue;
        /// <summary>金钱支出 —— s11_game_ai.h:485 gold_expenses</summary>
        public int goldExpenses;
        /// <summary>金钱净增 —— s11_game_ai.h:486 gold_income</summary>
        public int goldIncome;
        /// <summary>粮食收入 —— s11_game_ai.h:487 food_harvest</summary>
        public int foodHarvest;
        /// <summary>粮食支出 —— s11_game_ai.h:488 food_use</summary>
        public int foodUse;
        /// <summary>粮食净增 —— s11_game_ai.h:489 food_income</summary>
        public int foodIncome;
        /// <summary>据点状态 —— s11_game_ai.h:490 state</summary>
        public OutpostState state = OutpostState.Rear;
        /// <summary>本回合分配给本据点的行动力 —— s11_game_ai.h:491 action_point</summary>
        public int actionPoint;
        /// <summary>兵力上限（按粮食推算）—— s11_game_ai.h:492 max_troops</summary>
        public int maxTroops;
        /// <summary>部队数上限（按粮食推算）—— s11_game_ai.h:493 max_unit_count</summary>
        public int maxUnitCount;
        /// <summary>所需最小武将数 —— s11_game_ai.h:494 min_person_count</summary>
        public int minPersonCount;

        /// <summary>
        /// 据点优先级（0~16）。由 CorpsAI.UpdateDistrictData 计算，用于决定本回合谁先花行动力。
        /// 对齐出处：s11_game_ai_district.cpp:77-93 的局部数组 priority[]（原版是函数内临时数组，
        /// 这里挂到态势结构上，便于调试输出与后续据点排序复用）。
        /// </summary>
        public int priority = -1;
    }

    /// <summary>
    /// 据点 AI 的回合级运行时缓存。
    ///
    /// 对齐出处：s11_game_ai.h:543-562 的 GameAI 成员变量。
    /// 说明：原版是全局单例，一批数据同时只服务一个势力/军团/据点；
    ///       这里用静态容器复刻同样语义。任何退出点都要保证不残留（见 Reset）。
    /// </summary>
    public static class OutpostRuntime
    {
        /// <summary>原版 GoldReserveTurn —— s11_game_ai.h:120</summary>
        public const int GoldReserveTurn = 6;
        /// <summary>原版 FoodReserveTurn —— s11_game_ai.h:121</summary>
        public const int FoodReserveTurn = 2;
        /// <summary>原版 MaxOrderQueueSize —— s11_game_ai.h:122</summary>
        public const int MaxOrderQueueSize = 36;

        /// <summary>当前处理的势力（对齐 GameAI::force_）</summary>
        public static Force force;
        /// <summary>当前处理的军团（对齐 GameAI::corps 语义，原版由 district 参数传递）</summary>
        public static Corps corps;

        /// <summary>据点态势表，按 City.Id 索引（对齐 原版据点态势数组）</summary>
        public static readonly Dictionary<int, OutpostInfo> outposts = new Dictionary<int, OutpostInfo>();

        /// <summary>按状态统计的据点数量（对齐 原版据点状态计数）</summary>
        public static readonly int[] stateCount = new int[(int)OutpostState.Max];

        /// <summary>当前据点的可用行动力（对齐 GameAI::action_point_）</summary>
        public static int actionPoint;
        /// <summary>当前据点的可用金（对齐 GameAI::gold_）</summary>
        public static int gold;
        /// <summary>当前据点的可用粮（对齐 GameAI::food_）</summary>
        public static int food;
        /// <summary>当前据点可下令的武将（对齐 GameAI::idle_person_list_）</summary>
        public static readonly List<Person> idlePersonList = new List<Person>();
        /// <summary>当前据点的命令队列（对齐 GameAI::order_queue_ / order_count_）</summary>
        public static readonly List<AIOrderId> orderQueue = new List<AIOrderId>(MaxOrderQueueSize);

        /// <summary>本回合已施放的"离间"次数（对齐原版 AI 上下文的计数上限 2）</summary>
        public static int disruptCount;
        /// <summary>本回合已施放的"协助"次数（上限 1）</summary>
        public static int collaborateCount;
        /// <summary>本回合已施放的"流言"次数（上限 3）</summary>
        public static int rumorCount;

        /// <summary>
        /// 本回合该命令是否真的执行了动作（用于决定扣不扣行动力）。
        /// 原版每个 据点命令函数 在 commander_-&gt;xxx 返回 true 后立即扣 action_point_；
        /// 本项目命令体没有统一返回值语义，改由命令体在"真正执行"处显式置位。
        /// </summary>
        public static bool commandExecuted;

        /// <summary>
        /// 本回合该命令是否由**委托给 CityAI 的实现**真正执行了动作。
        ///
        /// 背景：据点命令分两类 ——
        ///   · 重写过的（Move / Summon / Transport / 计略 / 拆建筑 等）在命令体内自行扣行动力；
        ///   · 委托给 <c>CityAI</c> 的（开发 / 征兵 / 巡查 / 训练 / 攻击 / 建造 / 搜索 / 登用 / 褒赏 / 合并 等）
        ///     是给非据点场景写的，**不会**动 <see cref="actionPoint"/>，也不会调 <see cref="MarkExecuted"/>。
        /// 调度器据此补扣，避免这批命令让行动力门禁永久放行（等于无限行动力）。
        /// </summary>
        public static bool delegatedExecuted;

        /// <summary>
        /// 清空回合级缓存。对齐 GameAI::setup()（s11_game_ai.cpp:355-377）的清表动作。
        /// </summary>
        public static void Reset()
        {
            force = null;
            corps = null;
            outposts.Clear();
            for (int i = 0; i < stateCount.Length; i++)
                stateCount[i] = 0;
            actionPoint = 0;
            gold = 0;
            food = 0;
            idlePersonList.Clear();
            orderQueue.Clear();
            commandExecuted = false;
            delegatedExecuted = false;
            disruptCount = 0;
            collaborateCount = 0;
            rumorCount = 0;
        }

        /// <summary>
        /// 取得（或新建）指定据点的态势数据。
        /// </summary>
        /// <param name="cityId">据点 Id</param>
        /// <returns>态势数据</returns>
        public static OutpostInfo Get(int cityId)
        {
            OutpostInfo info;
            if (!outposts.TryGetValue(cityId, out info))
            {
                info = new OutpostInfo();
                outposts[cityId] = info;
            }
            return info;
        }

        /// <summary>
        /// 尝试取得指定据点的态势数据，不存在时返回 null。
        /// </summary>
        /// <param name="cityId">据点 Id</param>
        /// <returns>态势数据或 null</returns>
        public static OutpostInfo Find(int cityId)
        {
            OutpostInfo info;
            return outposts.TryGetValue(cityId, out info) ? info : null;
        }

        /// <summary>
        /// 标记"本条命令已真正执行动作"（供行动力扣减判定）。
        /// </summary>
        public static void MarkExecuted()
        {
            commandExecuted = true;
        }

        /// <summary>
        /// 标记"本条命令由 CityAI 委托实现执行了动作"（供调度器补扣行动力）。
        /// </summary>
        public static void MarkDelegated()
        {
            delegatedExecuted = true;
        }
    }
}
