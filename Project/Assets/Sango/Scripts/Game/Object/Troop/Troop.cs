/*
 * 文件名：Troop.cs
 * 描述：部队类，管理游戏中的部队对象
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Sango.Core.Action;
using Sango.Render;
using Sango.Tools;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 部队类，管理游戏中的部队对象
    /// 部队是游戏中的作战单位，包含主将、副将、兵力、士气等属性
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Troop : SangoObjectExtensionData
    {
        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override SangoObjectType ObjectType => SangoObjectType.Troops;

        /// <summary>
        /// 带颜色的部队名称
        /// </summary>
        public string ColorName => $"<color=#85B964>{Name}</color>";

        /// <summary>
        /// AI是否完成行动
        /// </summary>
        public virtual bool AIFinished { get; set; }

        /// <summary>
        /// AI是否准备完成
        /// </summary>
        public virtual bool AIPrepared { get; set; }

        /// <summary>
        /// 是否已经委任
        /// </summary>
        public bool IsAppoint => missionType > 0;

        /// <summary>
        /// 本部队当前使用的 <see cref="TroopAIType"/>（由"是否玩家第一军团"与"是否已委任"派生）。
        ///
        /// 判定口径：
        ///   · 非玩家第一军团（AI 势力部队、玩家势力非主军团部队）→ <see cref="TroopAIType.Faction"/>
        ///   · 玩家第一军团且已有任务                              → <see cref="TroopAIType.Appoint"/>
        ///   · 玩家第一军团且尚无任务                              → <see cref="TroopAIType.None"/>
        /// </summary>
        public TroopAIType AIType
        {
            get
            {
                if (!IsPlayerControl)
                    return TroopAIType.Faction;

                // 玩家第一军团：有任务即视为"已委任"，交给委任 AI 单纯执行；
                // 无任务则完全由玩家手动操控，AI 不介入。
                return IsAppoint ? TroopAIType.Appoint : TroopAIType.None;
            }
        }

        /// <summary>
        /// 势力缓存（**仅运行期，不入档**）。
        ///
        /// 为什么需要它：所属势力原本完全由主将实时推导（Leader?.BelongForce），
        /// 主将一旦脱离编制（阵亡 / 被俘 / 下野 / 被移出队伍）部队就立刻"没有势力"了，
        /// 而收尾链路恰好全都在这时读它：
        ///   · 破城解救俘虏（City.OnFall 的 person.IsSameForce(atk)）；
        ///   · 破城招降演出的分支判据（CityRecruitPersonWhenCityFallEvent 的 atk.BelongCorps.IsPlayerControl）；
        ///   · 部队溃灭抓捕（Troop.OnDestroy 的 IsEnemy(atkTroop)）、势力灭亡时清理无主部队（City.OnFall）；
        ///   · 各类战报日志（"{BelongForce?.Name}的[部队]溃灭"）。
        /// 主将已脱离时这些判断会集体落空 —— 典型表现就是"自家武将被自家城池当成俘虏关着"。
        ///
        /// 维护口径（三句话）：
        ///   1) 主将在位且势力非空：以主将为准，顺手把最新归属写进缓存（主将换势力 / 换军团后不会返回过期值）；
        ///   2) 主将被清空 / 顶替：在 Leader 的 setter 里先把**旧主将**的归属落进缓存，供之后兜底；
        ///   3) 主将在位但势力为空：如实返回 null，且不动缓存（脏数据不掩盖，交给上层报错定位）。
        /// </summary>
        Force mBelongForce;

        /// <summary>
        /// 军团缓存（**仅运行期，不入档**）。与 <see cref="mBelongForce"/> 同一口径。
        ///
        /// 势力兜底必须连军团一起兜住：只兜势力会出现"有势力、没军团"的错配，
        /// 而调用方几乎都是配对使用（atk.BelongCorps.IsPlayerControl、
        /// building.ChangeCorps(atk.BelongCorps)、City.OnFall 的 BelongCorps = atk.BelongCorps）→ 直接空引用。
        /// </summary>
        Corps mBelongCorps;

        /// <summary>
        /// 所属势力（主将实时推导；主将已脱离编制时用缓存兜底）
        /// </summary>
        public Force BelongForce
        {
            get
            {
                Force force;
                Corps corps;
                // 主将在位：以主将为准（SyncBelongCache 内部会同步缓存）
                if (SyncBelongCache(out force, out corps))
                    return force;

                // 主将已脱离：用最后一次已知归属兜底
                return mBelongForce;
            }
        }

        /// <summary>
        /// 所属军团（主将实时推导；主将已脱离编制时用缓存兜底）
        /// </summary>
        public Corps BelongCorps
        {
            get
            {
                Force force;
                Corps corps;
                // 主将在位：以主将为准（SyncBelongCache 内部会同步缓存）
                if (SyncBelongCache(out force, out corps))
                    return corps;

                // 主将已脱离：用最后一次已知归属兜底
                return mBelongCorps;
            }
        }

        /// <summary>
        /// 主将在位时把势力 / 军团同步进缓存；主将缺失时不写入，由调用方改用缓存兜底。
        /// </summary>
        /// <param name="force">主将当前势力（主将缺失时为 null）</param>
        /// <param name="corps">主将当前军团（主将缺失时为 null）</param>
        /// <returns>主将是否存在：true 时调用方以主将为准，false 时改用缓存兜底</returns>
        bool SyncBelongCache(out Force force, out Corps corps)
        {
            force = null;
            corps = null;

            Person leader = Leader;
            if (leader == null)
                return false;

            force = leader.BelongForce;
            corps = leader.BelongCorps;

            // 只在主将势力非空时刷新缓存：主将顶着空势力（脏数据）时不覆盖最后一次有效归属，
            // 保证他被清空后，兜底值仍是"最近一次真实归属"而不是 null
            if (force != null)
            {
                mBelongForce = force;
                mBelongCorps = corps;
            }

            return true;
        }

        /// <summary>
        /// Clear 是否已经执行过。Clear 是部队的最终收尾，可能被"兵力归零"与"武将全部离开"两条路各触发一次，
        /// 用它保证清理只走一遍（部队对象不会在 Clear 之后复用，Init 时会复位）。
        /// </summary>
        private bool isCleared;

        /// <summary>
        /// 所属城池（由主将**实时**推导）。
        /// 刻意不做缓存：主将换势力/换城后它必须立刻跟着变，缓存会拿到过期城池而摘错名单。
        /// </summary>
        public City BelongCity => Leader?.BelongCity;

        /// <summary>
        /// 所在城池
        /// </summary>
        public City CurrentCity
        {
            get
            {
                if (cell.BelongCity == null)
                {
                    Sango.Log.Error($"地块没有所属城池:{cell.x},{cell.y}!!!");
                    return Leader.BelongCity;
                }
                else
                {
                    return cell.BelongCity.BelongCity == null ? cell.BelongCity : cell.BelongCity.BelongCity;
                }
            }
        }


        /// <summary>
        /// 统领（主将）的 id。存档里只有这个数值，读取 Leader 时按需解析。
        /// </summary>
        [JsonProperty("Leader")]
        public int LeaderId;

        Person mLeader;
        /// <summary>
        /// 统领（主将）对象；写入时自动同步 LeaderId，并把旧主将的归属落进势力缓存
        /// </summary>
        public Person Leader
        {
            get
            {
                if (mLeader == null && LeaderId > 0) mLeader = IdRef.Resolve<Person>(LeaderId);
                return mLeader;
            }
            set
            {
                // 【势力缓存】主将换人 / 被清空前，先把**旧主将**的归属存进缓存：
                // 主将一被置空，BelongForce 就推导不出势力了，而收尾链路
                // （破城解救俘虏、溃灭抓捕、势力灭亡清无主部队、战报日志）都在这时读它。
                //
                // 这里刻意读 Leader 属性而不是 mLeader 字段：读档路径下主将还没被解析，
                // 直接读字段会拿到 null，白白漏掉这次缓存。
                Person old = Leader;
                if (old != null && old.BelongForce != null)
                {
                    mBelongForce = old.BelongForce;
                    mBelongCorps = old.BelongCorps;
                }

                mLeader = value;
                LeaderId = value != null ? value.Id : 0;
            }
        }

        /// <summary>
        /// 副将1 的 id（存档数值）
        /// </summary>
        [JsonProperty("Member1")]
        public int Member1Id;

        Person mMember1;
        /// <summary>
        /// 副将1 对象；写入时自动同步 Member1Id
        /// </summary>
        public Person Member1
        {
            get
            {
                if (mMember1 == null && Member1Id > 0) mMember1 = IdRef.Resolve<Person>(Member1Id);
                return mMember1;
            }
            set { mMember1 = value; Member1Id = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 副将2 的 id（存档数值）
        /// </summary>
        [JsonProperty("Member2")]
        public int Member2Id;

        Person mMember2;
        /// <summary>
        /// 副将2 对象；写入时自动同步 Member2Id
        /// </summary>
        public Person Member2
        {
            get
            {
                if (mMember2 == null && Member2Id > 0) mMember2 = IdRef.Resolve<Person>(Member2Id);
                return mMember2;
            }
            set { mMember2 = value; Member2Id = value != null ? value.Id : 0; }
        }


        /// <summary>
        /// 俘虏列表（运行期容器，不参与序列化）
        /// </summary>
        public SangoObjectList<Person> captiveList = new SangoObjectList<Person>();


        /// <summary>
        /// 部队名称缓存
        /// </summary>
        string _troopName;

        /// <summary>
        /// 部队名称
        /// </summary>
        public override string Name => _troopName;

        /// <summary>
        /// 所在格子的坐标（存档形态仍是 [x, y]）
        /// </summary>
        [JsonProperty("cell")]
        [JsonConverter(typeof(CellXYConverter))]
        public CellXY cellPos = CellXY.Invalid;

        Cell mCell;
        /// <summary>
        /// 所在格子；写入 Cell 时自动同步坐标
        /// </summary>
        public Cell cell
        {
            get
            {
                if (mCell == null) mCell = cellPos.ToCell();
                return mCell;
            }
            set { mCell = value; cellPos = CellXY.From(value); }
        }

        /// <summary>
        /// 坐标x
        /// </summary>
        public int x => cell?.x ?? 0;

        /// <summary>
        /// 坐标y
        /// </summary>
        public int y => cell?.y ?? 0;

        /// <summary>
        /// 当前兵力
        /// </summary>
        [JsonProperty] public int troops;

        /// <summary>
        /// 出征回合
        /// </summary>
        [JsonProperty] public int liveDays;

        /// <summary>
        /// 最大兵力
        /// </summary>
        public int MaxTroops { get; set; }

        /// <summary>
        /// 是否满兵
        /// </summary>
        public bool IsFull => troops >= MaxTroops;

        /// <summary>
        /// 当前伤兵
        /// </summary>
        [JsonProperty] public int woundedTroops;

        /// <summary>
        /// 战意
        /// </summary>
        [JsonProperty]
        public int energy;

        /// <summary>
        /// 士气
        /// </summary>
        [JsonProperty]
        public int morale;

        /// <summary>
        /// 最大士气
        /// </summary>
        public int MaxMorale => BelongCity?.MaxMorale ?? 100;

        /// <summary>
        /// 上次因"兄弟同心"推满气力的**季节编号**（见 <see cref="Scenario.SeasonStamp"/>）。
        /// 用于限制同一组兄弟部队每季度只结算一次（一年 4 季，每季 9 回合）。
        /// 初始值取 -1 而非 0：季节编号 = 年 × 4 + 季序号，实际永远大于等于 0，
        /// 因此开局第一次必然放行（若默认 0，第 0 年的第 1 季会被永久挡住）。
        /// </summary>
        [JsonProperty]
        public int swornCheerSeason = -1;

        /// <summary>
        /// "兄弟同心"对白是否正在进行、气力尚未真正加满的临时标记(不存档)。
        /// 加气走的是异步对白, 若不在攻击前把它跑完, 部队会一边播对白一边出手,
        /// 导致本次攻击吃不到刚推满的气力。SpellSkill 会在此标记为真时挂起, 等加气结束再出手。
        /// </summary>
        internal bool pendingSwornCheer = false;

        /// <summary>
        /// 移动能力
        /// </summary>
        public int MoveAbility => IsInWater ? waterMoveAbility : landMoveAbility;

        /// <summary>
        /// 水上移动能力
        /// </summary>
        public int waterMoveAbility;

        /// <summary>
        /// 陆地移动能力
        /// </summary>
        public int landMoveAbility;

        /// <summary>
        /// 携带粮食
        /// </summary>
        [JsonProperty] public int food;

        /// <summary>
        /// 携带金钱
        /// </summary>
        [JsonProperty] public int gold;

        /// <summary>
        /// 携带人口
        /// </summary>
        [JsonProperty] public int population;

        /// <summary>
        /// 携带道具
        /// </summary>
        [JsonProperty]
        public ItemStore itemStore = new ItemStore();

        /// <summary>
        /// 被影响的建筑,用来判断建筑影响唯一化
        /// </summary>
        public Dictionary<string, BuildingImproveBase> buildingImproveMap = new Dictionary<string, BuildingImproveBase>();


        public int captiveChangce = 0;

        /// <summary>
        /// 战法命中后触发单挑的概率(百分比)。
        /// 基础值取自剧本参数 skillDuelChance，在 CalculateAttribute 里求值，
        /// 之后可由修改型 Action（TroopChangeDuelChance）在 OnTroopCalculateAttribute 上二次修正。
        /// </summary>
        public int duelChance = 0;

        /// <summary>
        /// 部队辅佐概率(百分比)，初始为 0。
        /// CalculateAttribute 里每次复位为 0，之后由装配了"辅佐"特性的 Action(TroopAssistAttack)
        /// 在 OnTroopCalculateAttribute 上取 Max(不叠加)写入。
        /// Troop.CalcAssistChance 在关系档位(夫妇/义兄弟/亲爱/血亲)均未命中时按此值兜底判定援助概率。
        /// </summary>
        public int assistChance = 0;

        /// <summary>
        /// 是否行动完毕
        /// </summary>
        [JsonProperty]
        public override bool ActionOver
        {
            get => actionOver;
            set
            {
                if (actionOver != value)
                {
                    if (value)
                    {
                        if (ActionOverCount > 0)
                        {
                            ActionOverCount--;
                            isExtraAction = true;
                            return;
                        }
                    }
                    isExtraAction = false;
                    actionOver = value;
                    ActionOverCount = 0;
                    GameEvent.OnTroopActionOver?.Invoke(this);
                }
            }
        }
        private bool actionOver;
        // 额外行动的标志
        public bool isExtraAction = false;
        // 额外行动的次数
        public int ActionOverCount = 0;

        public bool IsPlayer => BelongForce?.IsPlayer ?? false;
        /// <summary>
        /// 是否为玩家控制的
        /// </summary>
        public virtual bool IsPlayerControl => BelongCorps?.IsPlayerControl ?? false;
        public bool IsCurPlayer => BelongForce?.IsCurPlayer ?? false;

        /// <summary>
        /// 当前任务类型
        /// </summary>
        [JsonProperty] public int missionType;

        /// <summary>
        /// 出征意图（见 <see cref="TroopDeployType"/>）。
        ///
        /// 【与原版的关系】对应原版 ScenarioAI 的 <c>context_-&gt;unit[].deploy_type</c>
        /// （s11_scen.h:3495-3503）。原版由据点级出征命令写入，部队层的
        /// <c>unit_update_order</c> 只读它做改派（s11_game_ai_unit.cpp:124-130）。
        ///
        /// 【本项目怎么填】出征入口在设置 missionType 的同时按任务性质推断一次
        /// （见 <see cref="ToDeployType"/>），之后**不再随 missionType 变化**——
        /// 意图是"出征时定下的目标性质"，任务只是当前执行到哪一步。
        /// </summary>
        [JsonProperty] public int deployType;

        /// <summary>
        /// 出征意图的目标 Id（城 / 建筑）。对应原版 <c>context_-&gt;unit[].deploy_target</c>。
        /// -1 表示无目标。
        /// </summary>
        [JsonProperty] public int deployTarget = -1;

        /// <summary>
        /// 本部队是**响应谁的援军请求**而来（求援发起势力的 Id；-1 = 不是援军）。
        /// 对应原版 <c>Unit::get_request_force_id()</c>（s11_scen.h 的 unit.request_force_）。
        ///
        /// 【与 <see cref="deployType"/> 的区别】deployType 是"这支部队想干什么"，
        /// 本字段是"这支部队欠谁的人情"——援军部队的改派约束更严：
        /// 原版 s11_game_ai_unit.cpp:114-123 对援军只做一件事，检查是否可以撤回。
        ///
        /// 【谁写入】同盟出兵时由派兵方设置为本方 Id；求援到期/撤回时清回 -1。
        /// </summary>
        [JsonProperty] public int requestForceId = -1;

        /// <summary>
        /// 按任务性质推断出征意图（出征入口写 <see cref="deployType"/> 时使用）。
        ///
        /// 映射依据是原版"据点 AI 派出部队时给的是哪种 deploy"：
        ///   · Attack    ← 占领 / 破坏部队 / 破坏建筑 / 骚扰 / 驱逐（kyoten_cmd_mil_attack）
        ///   · Defense   ← 守城 / 守建筑 / 护卫友军 / 跨城增援（kyoten_cmd_mil_defense）
        ///   · Transport ← 补给队 / 物资运输 / 求援（kyoten_cmd_mil_support）
        ///   · Build     ← 工程队 / 修补队（kyoten_cmd_mil_build）
        /// 未列出的任务（返城 / 待命）归 None，原版这两个也没有对应 deploy。
        /// </summary>
        public static int ToDeployType(int missionTypeValue)
        {
            switch ((MissionType)missionTypeValue)
            {
                case MissionType.TroopOccupyCity:
                case MissionType.TroopDestroyTroop:
                case MissionType.TroopDestroyBuilding:
                case MissionType.TroopHarassCity:
                case MissionType.TroopBanishTroop:
                    return (int)TroopDeployType.Attack;

                case MissionType.TroopProtectCity:
                case MissionType.TroopProtectBuilding:
                case MissionType.TroopProtectTroop:
                case MissionType.TroopMovetoCity:
                    return (int)TroopDeployType.Defense;

                case MissionType.TroopSupplyTroop:
                case MissionType.TroopTransformGoodsToCity:
                case MissionType.TroopAskSupply:
                    return (int)TroopDeployType.Transport;

                case MissionType.TroopBuildBuilding:
                case MissionType.TroopFixBuilding:
                    return (int)TroopDeployType.Build;

                default:
                    return (int)TroopDeployType.None;
            }
        }

        /// <summary>
        /// 任务目标
        /// </summary>
        [JsonProperty] public int missionTarget;

        /// <summary>
        /// 任务参数1
        /// </summary>
        [JsonProperty] public int missionParams1;

        /// <summary>
        /// 任务参数1
        /// </summary>
        [JsonProperty] public int missionParams2;

        /// <summary>
        /// 【随军增筑】就地修建辅助建筑前记录的原任务类型（0 表示当前不是"随军增筑"流程）。
        /// 建筑完工后据此恢复原任务，使作战部队不会因顺手修一座军乐台而永久脱离战线。
        /// 与专职工程队（不需要恢复任务）区分开。
        /// </summary>
        [JsonProperty] public int fieldBuildReturnMission;

        /// <summary>
        /// 【随军增筑】就地修建辅助建筑前记录的原任务目标 Id。
        /// 与 <see cref="fieldBuildReturnMission"/> 配对，用于恢复原任务的完整上下文。
        /// </summary>
        [JsonProperty] public int fieldBuildReturnTarget;

        /// <summary>
        /// 部队角色：决定"怎么打"（攻坚 / 防守 / 骚扰 / 游击 / 护卫 / 守备），
        /// 与 <see cref="missionType"/>（去哪）正交。
        /// 默认 <see cref="TroopRole.Auto"/>，由 <see cref="ResolveRole"/> 依据任务与自身属性自动推导。
        /// </summary>
        [JsonProperty] public TroopRole role = TroopRole.Auto;

        /// <summary>
        /// 当前状态管理器
        /// </summary>
        [JsonProperty]
        public BuffManager buffManager = new BuffManager();

        /// <summary>
        /// 部队**状态标记位**（位域，取值见 <see cref="TroopStateFlag"/>）。
        ///
        /// 【为什么是派生状态、不序列化】内容完全由 <see cref="buffManager"/> 里的 Buff 决定。
        /// 存进存档只会在"读档后 Buff 已过期但标记还在"这类情况下产生不一致，
        /// 因此每次增删 Buff 都调用 <see cref="RefreshStateFlags"/> 重算，热路径只读 bit。
        ///
        /// 【与 <see cref="pathFlags"/> 的区别】那个是"特技 / 科技给的寻路免疫开关"，
        /// 由持有者自己置位 / 清位；本字段是**部队当前状态**，只由 BuffManager 统一重算。
        /// </summary>
        int stateFlags;

        /// <summary>
        /// 是否处于**混乱**状态（原版 <c>UnitState_Konran</c>）。
        /// 混乱部队不向周围施加 ZOC（见 <see cref="TroopStateFlag.Chaos"/>）。
        /// </summary>
        public bool isChaos
        {
            get { return (stateFlags & (int)TroopStateFlag.Chaos) != 0; }
        }

        /// <summary>
        /// 按当前 Buff 列表重算 <see cref="stateFlags"/>。
        ///
        /// 【调用点】只由 <see cref="BuffManager"/> 在 Buff 增 / 删 / 到期 / 清空后调用，
        /// 业务代码不要手动置位 —— 手动置位一旦漏掉清位，就会留下"永久混乱"。
        /// </summary>
        public void RefreshStateFlags()
        {
            stateFlags = buffManager != null ? buffManager.GetStateFlags() : 0;
        }

        public void AddBuff(int id, int turnCount, Troop srcTroop)
        {
            // 【根因修复】buffManager 是 [JsonProperty] 字段：读档 / 新建时会被反序列化
            // 成新的 BuffManager，而它的 Master 只在 Troop.Init（:733 的 buffManager.Init(this)）
            // 里绑定。在"反序列化完成 → Troop.Init"这个窗口内发生的 AddBuff（技能 / 战斗触发）
            // 会带着 Master == null 进入 CreateAsset → target.GetRender() 空引用
            // （BuffManager.cs:27 线上 NRE 的来源）。
            // 这里在入口自愈：未绑定就先补绑，等价于把 Troop.Init 的那一步提前。
            if (buffManager == null)
                buffManager = new BuffManager();
            if (buffManager.Master == null)
                buffManager.Init(this);

            buffManager.AddBuff(id, turnCount, srcTroop);
        }
        public void RemoveBuff(int id) { buffManager.RemoveBuff(id); }
        public void RemoveBuffByKind(int kind) { buffManager.RemoveBuffByKind(kind); }

        public bool HasControlBuff()
        {
            return buffManager.HasControlBuff();
        }

        /// <summary>
        /// 队内**具备指定特技**的武将中，某项个人属性的最大值；无人具备则返回 -1。
        ///
        /// 【原版对应】MilitaryObject::get_best_member_stat(skillId, statType)
        /// （计略成功率公式里大量使用，例如"施法方火攻特技者的智力是否高于目标智力"）。
        /// </summary>
        /// <param name="featureId">特技 Id（Features.json）</param>
        /// <param name="statType">0=统率 1=武力 2=智力 3=政治 4=魅力</param>
        public int GetBestMemberFeatureStat(int featureId, int statType)
        {
            int best = -1;
            best = MaxStatWithFeature(Leader, featureId, statType, best);
            best = MaxStatWithFeature(Member1, featureId, statType, best);
            best = MaxStatWithFeature(Member2, featureId, statType, best);
            return best;
        }

        static int MaxStatWithFeature(Person person, int featureId, int statType, int best)
        {
            if (person == null || !person.HasFeatrue(featureId))
                return best;

            int value;
            switch (statType)
            {
                case 0: value = person.Command; break;
                case 1: value = person.Strength; break;
                case 2: value = person.Intelligence; break;
                case 3: value = person.Politics; break;
                default: value = person.Glamour; break;
            }
            return value > best ? value : best;
        }

        /// <summary>
        /// 是否处于**伪报**状态（原版 <c>UnitState_Gihou</c>）。
        ///
        /// 与 <see cref="HasControlBuff"/> 的区别：混乱（Stun）不算伪报。
        /// 用途：
        ///   · AI 评分降权 —— 原版 get_attack_damage_score 的上层 get_target_score
        ///     （s11_sys_unit_action.cpp:1739-1741）对伪报目标 <c>score *= 0.1</c>，
        ///     因为伪报部队会自己撤回城，打它等于浪费一次行动；
        ///   · 与反击判定共用同一套状态语义（原版 state != Normal 不能反击）。
        /// </summary>
        public bool IsEscapeState()
        {
            return buffManager.HasEscapeBuff();
        }

        /// <summary>
        /// 任务地点的坐标（存档形态仍是 [x, y]）
        /// </summary>
        [JsonProperty("missionTargetCell")]
        [JsonConverter(typeof(CellXYConverter))]
        public CellXY missionTargetCellPos = CellXY.Invalid;

        Cell mMissionTargetCell;
        /// <summary>
        /// 任务地点；写入 Cell 时自动同步坐标
        /// </summary>
        public Cell missionTargetCell
        {
            get
            {
                if (mMissionTargetCell == null) mMissionTargetCell = missionTargetCellPos.ToCell();
                return mMissionTargetCell;
            }
            set { mMissionTargetCell = value; missionTargetCellPos = CellXY.From(value); }
        }

        /// <summary>
        /// 【需求4】判断是否需要向附近的补给队求援：
        /// 1. 自身状态低于阈值（兵力 / 满编 小于 askSupplyHealthPercent）；
        /// 2. 范围内存在己方补给队（运输队），且该补给队尚有物资可移交。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <param name="supplier">找到的补给队（未找到时为 null）</param>
        /// <returns>是否需要求援</returns>
        public bool IsNeedAskSupply(Scenario scenario, out Troop supplier)
        {
            supplier = null;

            AIConfig cfg = AIConfig.Instance;
            if (cfg.askSupplyHealthPercent <= 0 || scenario == null || cell == null)
                return false;

            // 1) 状态检查：兵力低于满编阈值，或粮草即将告罄
            bool lowTroops = MaxTroops > 0 && troops * 100 < MaxTroops * cfg.askSupplyHealthPercent;
            bool lowFood = IsWithOutFood() == 1;
            if (!lowTroops && !lowFood)
                return false;

            // 2) 范围内是否有能补给的己方补给队
            int range = cfg.askSupplySearchRange;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop t = scenario.troopsSet[i];
                if (t == null || !t.IsAlive || t == this)
                    continue;
                if (!t.IsTransport || !t.IsSameForce(this) || t.cell == null)
                    continue;
                // 补给队自身必须还有物资可给
                if (t.food <= 0 && t.troops <= 0
                    && (t.itemStore == null || t.itemStore.TotalNumber <= 0))
                    continue;

                int distance = scenario.Map.Distance(cell, t.cell);
                if (distance > range)
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    supplier = t;
                }
            }

            return supplier != null;
        }

        public TroopType TroopType
        {
            get { return IsInWater ? WaterTroopType : LandTroopType; }
            set { LandTroopType = value; }
        }

        /// <summary>
        /// 兵种适应力
        /// </summary>
        public int TroopTypeLv => IsInWater ? WaterTroopTypeLv : LandTroopTypeLv;

        /// <summary>
        /// 水上部队类型的 id（存档数值），读取 WaterTroopType 时按需解析
        /// </summary>
        [JsonProperty("WaterTroopType")]
        public int WaterTroopTypeId;

        TroopType mWaterTroopType;
        /// <summary>
        /// 水上部队类型；写入时自动同步 WaterTroopTypeId
        /// </summary>
        public TroopType WaterTroopType
        {
            get
            {
                if (mWaterTroopType == null && WaterTroopTypeId > 0) mWaterTroopType = IdRef.Resolve<TroopType>(WaterTroopTypeId);
                return mWaterTroopType;
            }
            set { mWaterTroopType = value; WaterTroopTypeId = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 陆上部队类型的 id（存档数值），读取 LandTroopType 时按需解析
        /// </summary>
        [JsonProperty("LandTroopType")]
        public int LandTroopTypeId;

        TroopType mLandTroopType;
        /// <summary>
        /// 陆上部队类型；写入时自动同步 LandTroopTypeId
        /// </summary>
        public TroopType LandTroopType
        {
            get
            {
                if (mLandTroopType == null && LandTroopTypeId > 0) mLandTroopType = IdRef.Resolve<TroopType>(LandTroopTypeId);
                return mLandTroopType;
            }
            set { mLandTroopType = value; LandTroopTypeId = value != null ? value.Id : 0; }
        }

        public int WaterTroopTypeLv { get; private set; }
        public int LandTroopTypeLv { get; private set; }

        /// <summary>
        /// 是否在水中
        /// </summary>
        /// <returns></returns>
        public bool IsInWater => cell?.TerrainType.isWater ?? false;


        public List<SkillInstance> skills => IsInWater ? waterSkills : landSkills;

        /// <summary>
        /// 当前技能
        /// </summary>
        //[JsonProperty]
        public List<SkillInstance> waterSkills;

        /// <summary>
        /// 当前技能
        /// </summary>
        //[JsonProperty]
        public List<SkillInstance> landSkills;

        /// <summary>
        /// 计略
        /// </summary>
        public List<SkillInstance> StrategySkills = new List<SkillInstance>();

        // 近战普攻
        public SkillInstance NormalSkill => IsInWater ? waterNormalSkill : landNormalSkill;
        SkillInstance waterNormalSkill;
        SkillInstance landNormalSkill;

        // 远程普攻
        public SkillInstance NormalRangeSkill => IsInWater ? waterNormalRangeSkill : landNormalRangeSkill;
        SkillInstance waterNormalRangeSkill;
        SkillInstance landNormalRangeSkill;

        /// <summary>
        /// 对部队的额外伤害增减
        /// </summary>
        public float DamageTroopExtraFactor => IsInWater ? waterDamageTroopExtraFactor : landDamageTroopExtraFactor;
        public float waterDamageTroopExtraFactor;
        public float landDamageTroopExtraFactor;
        /// <summary>
        /// 对建筑的额外伤害增益
        /// </summary>
        public float DamageBuildingExtraFactor => IsInWater ? waterDamageBuildingExtraFactor : landDamageBuildingExtraFactor;
        public float waterDamageBuildingExtraFactor;
        public float landDamageBuildingExtraFactor;


        public int defeatTroopCanGainFoodFactor;
        public int defeatTroopCanGainGoldFactor;

        public int SpearLv { get; private set; }
        public int HalberdLv { get; private set; }
        public int CrossbowLv { get; private set; }
        public int RideLv { get; private set; }
        public int WaterLv { get; private set; }
        public int MachineLv { get; private set; }

        /// <summary>
        /// 攻击力。
        ///
        /// 【末乘 Buff 系数】对应原版 unit_calc_stat 的 <c>state_coef</c>（混乱 ×0.8），
        /// 但本项目不写死状态分支：由每个 Buff 自己声明 <c>troopAttackCoef</c>，这里累乘。
        /// 因为 Attack 是表达式属性，Buff 增删后立即生效，无需等待重算。
        /// </summary>
        public int Attack => (extraAttack + (IsInWater ? waterAttack : landAttack) + trrainBonusAtk)
                             * buffManager.GetTroopAttackCoef() / 100;

        public int waterAttack;
        public int landAttack;
        public int extraAttack;
        public int trrainBonusAtk;
        /// <summary>
        /// 防御力。末乘 Buff 系数（同 Attack，对应原版 state_coef）。
        /// </summary>
        public int Defence => (extraDefence + (IsInWater ? waterDefence : landDefence) + trrainBonusDef)
                              * buffManager.GetTroopDefenceCoef() / 100;

        public int waterDefence;
        public int landDefence;
        public int extraDefence;
        public int trrainBonusDef;

        /// <summary>
        /// 建设力。末乘 Buff 系数（对应原版 state_coef，混乱时同样 ×0.8）。
        /// </summary>
        public int BuildPower
        {
            get { return buildPower * buffManager.GetTroopBuildCoef() / 100; }
            set { buildPower = value; }
        }

        /// <summary>
        /// 建设力的**基础值**（不乘 Buff 系数）。
        ///
        /// 【为什么需要】`TroopImproveBuildPower` 这类 Action 是"读-改-写"
        /// （`troop.BuildPower = troop.BuildPower * value / 100`）。
        /// 如果读的是带 Buff 系数的 <see cref="BuildPower"/>，再写回去就会把临时系数
        /// **固化进基础值**，下次 get 又乘一遍 —— 混乱状态下反复触发会持续缩水。
        /// 所以读改写一律走本属性。
        /// </summary>
        public int BuildPowerRaw
        {
            get { return buildPower; }
            set { buildPower = value; }
        }

        int buildPower;

        /// <summary>
        /// 统率
        /// </summary>
        public int Command { get; private set; }

        /// <summary>
        /// 武力
        /// </summary>
        public int Strength { get; private set; }

        /// <summary>
        /// 智力
        /// </summary>
        public int Intelligence { get; private set; }

        /// <summary>
        /// 政治
        /// </summary>
        public int Politics { get; private set; }

        /// <summary>
        /// 魅力
        /// </summary>
        public int Glamour { get; private set; }

        /// <summary>
        /// 是否忽略陆地 ZOC。底层是 <see cref="pathFlags"/> 的位标记，
        /// 保留成属性是为了让 `Map.IsZOC`、`TroopIgnoreZOC` 等既有调用点不用改。
        /// </summary>
        public bool ignoreLandZOC
        {
            get { return (pathFlags & (int)PathFlag.IgnoreLandZoc) != 0; }
            set
            {
                if (value) pathFlags |= (int)PathFlag.IgnoreLandZoc;
                else pathFlags &= ~(int)PathFlag.IgnoreLandZoc;
            }
        }

        /// <summary>是否忽略水上 ZOC（原版 ZOC 分水陆两套，不能合并成一个标记）</summary>
        public bool ignoreWaterZOC
        {
            get { return (pathFlags & (int)PathFlag.IgnoreWaterZoc) != 0; }
            set
            {
                if (value) pathFlags |= (int)PathFlag.IgnoreWaterZoc;
                else pathFlags &= ~(int)PathFlag.IgnoreWaterZoc;
            }
        }

        /// <summary>是否免疫火焰（着火格不再 ×4 消耗）</summary>
        public bool ignoreFire
        {
            get { return (pathFlags & (int)PathFlag.IgnoreFire) != 0; }
            set
            {
                if (value) pathFlags |= (int)PathFlag.IgnoreFire;
                else pathFlags &= ~(int)PathFlag.IgnoreFire;
            }
        }

        /// <summary>
        /// 是否免疫毒泉（移动不再损失兵力、寻路不再付额外代价）。
        /// 标记由「解毒」特技（或其他来源）置位；`TerrainType.moveImmunityFlag` 读的也是这一位。
        /// </summary>
        public bool ignorePoison
        {
            get { return (pathFlags & (int)PathFlag.IgnorePoison) != 0; }
            set
            {
                if (value) pathFlags |= (int)PathFlag.IgnorePoison;
                else pathFlags &= ~(int)PathFlag.IgnorePoison;
            }
        }

        /// <summary>
        /// 是否免疫栈道（移动不再损失兵力、寻路不再付额外代价）。
        /// 标记由「踏破」特技 / 「藤甲」特技 / 「难所行军」科技置位。
        /// </summary>
        public bool ignoreCliffroad
        {
            get { return (pathFlags & (int)PathFlag.IgnoreCliffroad) != 0; }
            set
            {
                if (value) pathFlags |= (int)PathFlag.IgnoreCliffroad;
                else pathFlags &= ~(int)PathFlag.IgnoreCliffroad;
            }
        }

        public bool IsJustCreated => x == BelongCity.x && y == BelongCity.y;

        public override ObjectRender GetRender() { return Render; }
        public TroopRender Render { get; private set; }
        bool isMissionPrepared = false;
        /// <summary>
        /// 【出城兜底】标记任务已结束、但出城移动动画尚未播完。
        /// 用于任务执行后的保底出城：下一帧继续完成出城，而不再重跑任务行为。
        /// </summary>
        bool leavingBuildingCell = false;
        public int foodCost = 0;
        public float foodCostFactor = 1;

        public List<ActionBase> actionList;
        public List<Cell> MoveRange = new List<Cell>(256);

        /// <summary>
        /// 寻路标记位（位域，取值见 <see cref="PathFlag"/>）。
        ///
        /// 【为什么放在 Troop 上】原版 `UnitPath` 的这批开关是**按部队**生效的
        /// （解毒特技、难所行军科技、飞将类无视 ZOC），不是全局设置；
        /// 放在部队上，AI 与玩家各用各的，也不会互相污染。
        ///
        /// 【与技能的关系】由技能/科技持有的 Effect 在「本回合生效」时置位（见 PathFlag 注释）。
        ///
        /// 【为什么不序列化】它是**派生状态**，来源是技能/科技/Buff，
        /// 存进存档只会带来"读档后残留上回合的标记"这类不一致。
        ///
        /// 【生命周期】**不做定期清理**：置位/清位都由持有者负责 ——
        /// 特技或 action（如 `TroopIgnoreZOC`）在效果生效时置位、失效时清位；
        /// 属性 getter 只在计算时读取当前值。任何"按回合统一清零"都会与
        /// "先算范围、后结算效果"的调用顺序打架。
        /// </summary>
        public int pathFlags;

        static List<Feature> temp_FeatureList = new List<Feature>();
        public void InitActionList()
        {
            // 【泄漏修复】已收尾的部队不再装配 Action。
            //
            // 收尾（阵亡 / 入城 / 解散 → ClearWithBelongCity）之后 isCleared 为 true，
            // 此后的 Clear() 会在开头直接 return。如果这时还有路径调用本函数
            // （ResetActionAndStatus 的几个调用点：武将从部队离开 Person.cs:1814、
            //   转投势力 Troop.cs:2933、编辑武将窗口 UIPersonEdit.cs:256），
            // 新装配出来的 Action 订阅就**再也退不掉**了：
            //   · TroopAddMoveAbility / TroopAddSkill 订阅 OnTroopCalculateAttribute
            //   · TroopChangeDamage 订阅 OnTroopChangeTroops
            // 它们会带着旧剧本的订阅活到下一次开局，在 GameEventDiagnostics 的
            // shutdown-begin 跨次快照里逐轮累积（实测每次 +3 左右）。
            if (isCleared)
                return;

            temp_FeatureList.Clear();
            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();

                actionList.Clear();
            }
            else
                actionList = new List<ActionBase>();

            ForEachPerson(x =>
            {
                if (x.FeatureList != null)
                {
                    for (int i = 0; i < x.ActiveFeatureCount; i++)
                    {
                        Feature feature = x.FeatureList[i];
                        if (feature != null && feature.kind <= (int)FeatureKindType.TroopSupport)
                        {
                            if (!feature.only)
                            {
                                temp_FeatureList.Add(feature);
                                feature.InitActions(actionList, this, x);
                            }
                            else
                            {
                                if (!temp_FeatureList.Contains(feature))
                                {
                                    temp_FeatureList.Add(feature);
                                    feature.InitActions(actionList, this, x);
                                }
                            }
                        }
                    }
                }
            });
        }


        public override void Init(Scenario scenario)
        {
            isCleared = false;

            // 【势力缓存复位】Init 是部队对象的初始化入口（新建部队 / 读档重建都走这里），
            // 先把上一次生命周期可能残留的缓存清掉，再按当前主将建立一次：
            //   · 新建部队：Leader 通常已由调用方赋值（走 setter），这里同步一次同样成立；
            //   · 读档：Leader 由 LeaderId 懒解析，这里主动触发一次解析并把归属写进缓存；
            //   · 主将暂时解析不出来也不影响：此后每次访问 BelongForce / BelongCorps 都会重试同步。
            mBelongForce = null;
            mBelongCorps = null;
            Force cacheForce;
            Corps cacheCorps;
            SyncBelongCache(out cacheForce, out cacheCorps);

            // 【委任部队】旧存档归一化：把玩家第一军团部队的 TroopXXX 任务
            // 转成 PlayerTroopXXX（详见 NormalizeAppointMission 的注释）。
            // 必须放在 Init 靠前位置，避免后续逻辑先按旧枚举建好了行为对象。
            NormalizeAppointMission();

            _troopName = $"{Leader?.Name}";
            ForEachPerson(x => x.mBelongTroop = this);
            InitActionList();

            CalculateAttribute(scenario);

            // 【无城部队】异族 / 贼的根据地部队（原版 `create_unit(nejiro, ...)`，
            // s11_sys_scen_unit.cpp:13-42）没有"所属城"：主将的 service 是**建筑**（根城），
            // 不是城；项目的 BelongCity 派生自主将 BelongCity（Troop.cs:173），因此为 null。
            //
            // 这两个名单只是"按城统计部队"的缓存（城面板 / 城防统计用），
            // 没有城就没有要登记的城。Clear() 与 City.RebuildTroopLists 早已按 null 安全处理，
            // 这里补齐同样的判空，否则创建部队第一帧就 NullReference。
            City belongCity = BelongCity;
            if (belongCity != null)
            {
                if (LandTroopType != null && LandTroopType.isFight && LandTroopType.Id != 1)
                    belongCity.allAttackTroops.Add(this);
                belongCity.allTroops.Add(this);
            }
            cell.troop = this;
            Render = new TroopRender(this);
            foodCost = (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (troops + woundedTroops) * TroopType.foodCostFactor);
            foodCost = (int)Math.Ceiling(foodCost * foodCostFactor);

            if (captiveList.Count > 0)
            {
                captiveList.RemoveAll(x => x.mBelongTroop != this);
                // 【性能】原 ForEach(lambda) 捕获无外部局部，但仍会分配委托；改为按下标遍历。
                for (int i = 0; i < captiveList.Count; i++)
                {
                    Person person = captiveList[i];
                    if (person == null)
                        continue;
                    // 去重：BeCaptiveList 是普通 List，重复添加不会报错但会让
                    // ForceCaptiveLoyaltyChange 对同一俘虏掷两次掉忠骰。
                    // 读档 + 重新初始化可能多次走到这里。
                    if (person.BelongForce != null && !person.BelongForce.BeCaptiveList.Contains(person))
                        person.BelongForce.BeCaptiveList.Add(person);
                }
            }

            buffManager.Init(this);
            GameEvent.OnTroopEnterCell?.Invoke(this, cell, null);
            UpdateTerrainBonus(cell);
        }
        public void ResetActionAndStatus()
        {
            // 已收尾的部队不必重算：InitActionList 里有同样的守卫，
            // 这里再拦一次，避免对已经不在剧本里的部队做无用的属性重算。
            if (isCleared)
                return;

            InitActionList();
            CalculateAttribute(Scenario.Cur);
        }

        public virtual bool Run(Corps corps, Force force, Scenario scenario)
        {
            return false;
            //yield return Event.OnTroopsStart?.Invoke(this, corps, force, scenario);
            //yield return Event.OnTroopsAI?.Invoke(this, corps, force, scenario);
            //yield return Event.OnTroopsEnd?.Invoke(this, corps, force, scenario);
        }
        public override void OnScenarioPrepare(Scenario scenario)
        {

            //foreach (SkillInstance s in landSkills)
            //{
            //    s.Init(this, s.skill);
            //}
            //foreach (SkillInstance s in waterSkills)
            //{
            //    s.Init(this, s.skill);
            //}
            // 【兜底】读档时先修复"主将已空、副将仍在"的中间态：
            // 主将为空会让 BelongCity 推导为 null，任务取值器不再派任务，部队会永久停摆。
            // 放在这里正合适：本方法早于 Troop.Init，Init 会按接任后的主将取名与算属性。
            TryStepUpMemberToLeader();
            if (Leader == null)
            {
                Sango.Log.Warning($"[{Name}]部队读档后既没有主将也没有副将，无法行动（坐标 {cell?.x},{cell?.y}）");
            }

            PrepeareFoodCost();
        }

        public int PrepeareFoodCost()
        {
            Scenario scenario = Scenario.Cur;
            foodCost = (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (troops + woundedTroops) * TroopType.foodCostFactor);
            foodCost = (int)Math.Ceiling(foodCost * foodCostFactor);

            // 着火烧粮 —— 原版 get_unit_food_use（s11_sys_scen_unit.cpp:1505-1512）：
            //   在着火格上时，额外消耗 = 现有兵粮 × (6 - 政治 × 0.05)%
            // 直接并入每回合粮耗（原版 int n = troops×c×0.05 + burned）。
            if (cell != null && cell.fire != null && food > 0)
            {
                float burned = food * (6f - Politics * 0.05f) * 0.01f;
                if (burned > 0)
                    foodCost += (int)burned;
            }

            if (foodCost <= 0 && troops > 0)
                foodCost = 1;

            return foodCost;
        }
        public int PrepeareFoodCost(int target)
        {
            Scenario scenario = Scenario.Cur;
            foodCost = (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInTroop * (target + woundedTroops) * TroopType.foodCostFactor);
            foodCost = (int)Math.Ceiling(foodCost * foodCostFactor);
            return foodCost;
        }

        public bool IsNewTroop => liveDays == 0;

        public override bool OnForceTurnStart(Scenario scenario)
        {
            liveDays++;
            MoveRange.Clear();

            // 【不做清理】pathFlags 由特技 / action（如 TroopIgnoreZOC）自己维护生命周期：
            // 置位与清位都发生在持有者那里（效果生效时置、失效时清）。
            // 这里若统一清零，会在"先算移动范围、后生效效果"的顺序下把刚置的位抹掉。
            ActionOver = false;
            AIFinished = false;
            AIPrepared = false;
            isMoving = false;
            isMissionPrepared = false;
            leavingBuildingCell = false;
            skillRenderEvent = null;
            skillRenderEventIsAssist = false;
            actionRenderEvent = null;
            moveRenderEvent = null;
            troopMissionBehaviour = null;
            if (food <= 0)
            {
                // 伤兵直接抛弃
                woundedTroops = 0;
                // 减少士气
                morale = (int)System.Math.Ceiling(morale * 0.3f);
                if (morale < 0)
                    morale = 0;

                // 原版 get_starving_troops（s11_game_week.cpp:823-829）：
                //     n = 兵力 × (30 - 魅力 / 15)%
                //     下限 400 + rand(200)
                //   兵力归零 → unit_disband_with_message（解散）
                //
                // 旧实现是"固定 30% 流失 + 兵力<500 直接解散"，与魅力无关，
                // 且小部队会被提前解散（原版还能靠 400 下限活下来）。
                int escapeRate = 30 - Glamour / 15;
                if (escapeRate < 0) escapeRate = 0;

                int damage = troops * escapeRate / 100;
                int minDamage = 400 + GameRandom.Range(0, 200);
                if (damage < minDamage) damage = minDamage;

                troops -= damage;
                if (troops <= 0)
                {
                    troops = 0;
                    Clear();
                    return true;
                }
            }
            else
            {
                PrepeareFoodCost();
                ChangeFood(-foodCost, false);
            }

            buffManager.OnForceTurnStart(scenario);

            foreach (SkillInstance skillInstance in waterSkills)
                skillInstance.OnForceTurnStart(scenario);
            foreach (SkillInstance skillInstance in landSkills)
                skillInstance.OnForceTurnStart(scenario);

            GameEvent.OnTroopTurnStart?.Invoke(this, scenario);

            if (Render != null)
            {
                Render.UpdateRender();
            }

            // 俘虏羁押天数累积
            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                person.missionCounter++;
            }

            return true;
        }
        public override bool OnForceTurnEnd(Scenario scenario)
        {
            // 计算俘虏越狱
            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                if (person == null) continue;

                if (GameRandom.Chance(GameFormula.Instance.PersonEscapeProbablility_InTroop(person, this, scenario), 10000))
                {
                    person.Escape(EscapeType.Escape);
                    if (IsPlayer)
                    {
                        PersonEscapeEvent personEscapeEvent = new PersonEscapeEvent()
                        {
                            person = person
                        };
                        RenderEvent.Instance.Add(personEscapeEvent);
                    }
                    Sango.Log.Info($"{person.Name}逃跑!");
                }
            }
            GameEvent.OnTroopTurnEnd?.Invoke(this, scenario);
            return true;
        }

        public int IsWithOutFood()
        {
            if (food == 0) return 0;
            if (food < foodCost * 4) return 1;
            return 2;
        }

        public void ForEachMember(Action<Person> action)
        {
            if (Member1 != null) action(Member1);
            if (Member2 != null) action(Member2);
        }

        public void ForEachPerson(Action<Person> action)
        {
            if (Leader != null) action(Leader);
            if (Member1 != null) action(Member1);
            if (Member2 != null) action(Member2);
        }

        /// <summary>
        /// 该武将是否登记在本部队里（主将 / 副将1 / 副将2）。
        ///
        /// 判"同部队"用它而不是 <c>person.mBelongTroop</c>：后者在部队收尾（ClearWithBelongCity）、
        /// 俘虏流程（Troop.AddCaptive）里会被清空或改写，而特技（如 #53 护卫）要的是
        /// "此时此刻还在不在这个编制里"，用编制成员判定才不会被这些中间状态影响。
        /// </summary>
        /// <param name="person">待判定的武将</param>
        /// <returns>是否为本部队成员</returns>
        public bool HasPerson(Person person)
        {
            if (person == null) return false;
            return person == Leader || person == Member1 || person == Member2;
        }

        /// <summary>
        /// 计算属性
        /// </summary>
        public void CalculateAttribute(Scenario scenario)
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            captiveChangce = Variables.captureChangceWhenTroopFall;
            // 单挑概率：基础值来自剧本参数，之后由 OnTroopCalculateAttribute 上的修改型 Action 二次修正
            duelChance = Variables.skillDuelChance;
            // 辅佐概率：每次计算先复位为 0，随后由 TroopAssistAttack 在 OnTroopCalculateAttribute 上取 Max 写入，
            // 保证武将变动/换阵后概率跟随重算，且不会跨次叠加
            assistChance = 0;

            StrategySkills.Clear();
            for (int i = 0; i < Variables.defaultStrategySkills.Length; i++)
            {
                Skill skill = scenario.CommonData.Skills.Get(Variables.defaultStrategySkills[i]);
                if (skill == null) continue;
                StrategySkills.Add(SkillInstance.Create(this, skill));
            }

            if (WaterTroopType == null)
                WaterTroopType = scenario.GetObject<TroopType>(8);

            defeatTroopCanGainFoodFactor = Variables.defeatTroopCanGainFoodFactor;
            defeatTroopCanGainGoldFactor = Variables.defeatTroopCanGainGoldFactor;

            LandTroopTypeLv = -1;
            WaterTroopTypeLv = -1;
            Command = -1;
            Strength = -1;
            Intelligence = -1;
            Politics = -1;
            Glamour = -1;
            // 计算能力,能力取最大
            ForEachPerson((p) =>
            {
                Command = System.Math.Max(Command, p.Command);
                Strength = System.Math.Max(Strength, p.Strength);
                Intelligence = System.Math.Max(Intelligence, p.Intelligence);
                Politics = System.Math.Max(Politics, p.Politics);
                Glamour = System.Math.Max(Glamour, p.Glamour);
                LandTroopTypeLv = System.Math.Max(LandTroopTypeLv, CheckTroopTypeLevel(LandTroopType, p));
                WaterTroopTypeLv = System.Math.Max(WaterTroopTypeLv, p.WaterLv);
                p.escapeFactorWhenTroopDestroy = 0;
            });

            // 兜底：能力最小为 1。
            //
            // 上面是"初始 -1 + 取全队最大"，因此小于 1 只有两种来历：
            //   · 队里没有任何武将（ForEachPerson 空转）→ 初始的 -1 被留下来；
            //   · 队里武将自己的能力值就是 0（数据缺失：未初始化的新建/临时武将等）→ max(-1, 0) = 0。
            // 两者都会让"把能力当除数"的公式出事（例如战法成功率 V2 的分母 A智²+B智²，
            // 见 SkillSuccessMethod.CalcIntelligenceRatioPart 的除零修复），也会让把能力当乘数的公式失真。
            // 这里统一夹到最小 1，作为最后一道防线（正常的 -1 哨兵值全工程无人依赖，已核对）。
            Command = System.Math.Max(Command, 1);
            Strength = System.Math.Max(Strength, 1);
            Intelligence = System.Math.Max(Intelligence, 1);
            Politics = System.Math.Max(Politics, 1);
            Glamour = System.Math.Max(Glamour, 1);

            List<SkillInstance> skillInstances = new List<SkillInstance>();
            landNormalSkill = null;
            waterNormalSkill = null;
            landNormalRangeSkill = null;
            waterNormalRangeSkill = null;

            if (LandTroopType.skills != null)
            { // 准备技能
                for (int i = 0; i < LandTroopType.skills.Length; i++)
                {
                    Skill skill = Scenario.Cur.GetObject<Skill>(LandTroopType.skills[i]);
                    if (skill != null && skill.CanAddToTroop(this, false))
                    {
                        skillInstances.Add(SkillInstance.Create(this, skill));
                    }
                }
            }
            landSkills = skillInstances;

            skillInstances = new List<SkillInstance>();
            if (WaterTroopType.skills != null)
            {
                for (int i = 0; i < WaterTroopType.skills.Length; i++)
                {
                    Skill skill = Scenario.Cur.GetObject<Skill>(WaterTroopType.skills[i]);
                    if (skill != null && skill.CanAddToTroop(this, true))
                    {
                        skillInstances.Add(SkillInstance.Create(this, skill));
                    }
                }
            }
            waterSkills = skillInstances;

            // 防御力 = (70%统率+30%智力) * 兵种防御力 / 100 * 适应力加成(A为1)
            landDefence = TroopsLevelBoost((
                Command * Variables.fight_troop_defence_command_factor
                + Strength * Variables.fight_troop_defence_strength_factor
                + Intelligence * Variables.fight_troop_defence_intelligence_factor
                + Politics * Variables.fight_troop_defence_politics_factor
                + Glamour * Variables.fight_troop_defence_glamour_factor
                ) / 10000 * LandTroopType.def, LandTroopTypeLv) / 100;

            // 攻击力 = (70%武力+30%统率) * 兵种攻击力 / 100 * 适应力加成(A为1)
            landAttack = TroopsLevelBoost((
                 Command * Variables.fight_troop_attack_command_factor
                + Strength * Variables.fight_troop_attack_strength_factor
                + Intelligence * Variables.fight_troop_attack_intelligence_factor
                + Politics * Variables.fight_troop_attack_politics_factor
                + Glamour * Variables.fight_troop_attack_glamour_factor
                ) / 10000 * LandTroopType.atk, LandTroopTypeLv) / 100;


            // 防御力 = (70%统率+30%智力) * 兵种防御力 / 100 * 适应力加成(A为1)
            waterDefence = TroopsLevelBoost((
                Command * Variables.fight_troop_defence_command_factor
                + Strength * Variables.fight_troop_defence_strength_factor
                + Intelligence * Variables.fight_troop_defence_intelligence_factor
                + Politics * Variables.fight_troop_defence_politics_factor
                + Glamour * Variables.fight_troop_defence_glamour_factor
                ) / 10000 * WaterTroopType.def, WaterTroopTypeLv) / 100;

            // 攻击力 = (70%武力+30%统率) * 兵种攻击力 / 100 * 适应力加成(A为1)
            waterAttack = TroopsLevelBoost((
                 Command * Variables.fight_troop_attack_command_factor
                + Strength * Variables.fight_troop_attack_strength_factor
                + Intelligence * Variables.fight_troop_attack_intelligence_factor
                + Politics * Variables.fight_troop_attack_politics_factor
                + Glamour * Variables.fight_troop_attack_glamour_factor
                ) / 10000 * WaterTroopType.atk, WaterTroopTypeLv) / 100;


            // 建设能力 = 政治 * 67% + 50;
            BuildPower = Politics * 2 / 3 + 50;

            waterMoveAbility = WaterTroopType.move;
            landMoveAbility = LandTroopType.move;

            CalculateMaxTroops(scenario);

            // 事件可二次修改属性
            GameEvent.OnTroopBeforeCalculateAttribute?.Invoke(this, scenario);
            GameEvent.OnTroopCalculateAttribute?.Invoke(this, scenario);
            GameEvent.OnTroopAfterCalculateAttribute?.Invoke(this, scenario);

            for (int i = 0; i < landSkills.Count; i++)
            {
                SkillInstance ins = landSkills[i];
                if (ins.IsNormal())
                {
                    if (ins.IsRange())
                        landNormalRangeSkill = ins;
                    else
                        landNormalSkill = ins;
                }
            }

            for (int i = 0; i < waterSkills.Count; i++)
            {
                SkillInstance ins = waterSkills[i];
                if (ins.IsNormal())
                {
                    if (ins.IsRange())
                        waterNormalRangeSkill = ins;
                    else
                        waterNormalSkill = ins;
                }
            }
        }

        public void CalculateMaxTroops(Scenario scenario)
        {
            if (LandTroopType.IsTransport())
            {
                MaxTroops = scenario.Variables.TransportTroopLimit;
                return;
            }

            int max = Leader.TroopsLimit;
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(max);
            GameEvent.OnTroopCalculateMaxTroops?.Invoke(Leader.BelongCity, this, overrideData);
            MaxTroops = overrideData.ValueAndRecycle;
        }

        /// <summary>
        /// 部队是否带伤：队内任一武将伤病等级 &gt; 0 即为真。
        ///
        /// 【口径】部队不单独存"伤病快照"，直接溯源到队内武将的 <see cref="Person.injury"/>：
        /// 五个能力 getter 已经过 Person.ApplyInjuryDecay 折算，CalculateAttribute 取的又是折算后的值，
        /// 所以只要武将伤病一变、部队重算一次，攻击/防御/建设力就自动是带伤后的数值（见 Person.OnInjuryChanged）。
        ///
        /// 【与原版的关系】原版 Unit::is_injured()（s11_scen_obj_unit.cpp:1155-1168）是
        /// "当前 ability_ 小于 max_ability_" 的判定，但**全工程没有任何调用点**（死代码）。
        /// 本项目按"不实现死代码分支"的约定不做 max_ability 快照，只保留这个派生查询给
        /// UI / AI / 日志使用，不参与任何战斗公式。
        /// </summary>
        public bool IsInjured
        {
            get
            {
                bool injured = false;
                ForEachPerson(p =>
                {
                    if (!injured && p != null && p.injury > 0) injured = true;
                });
                return injured;
            }
        }

        public int MoveCost(Cell cell)
        {
            if (cell.IsWater)
                return WaterTroopType.MoveCost(cell);
            else
                return LandTroopType.MoveCost(cell);
        }

        /// <summary>
        /// 部队到达指定格可用（或需要）的移动力。
        ///
        /// 【末乘 Buff 系数】用于"足止"类状态：<c>troopMoveCoef = 0</c> 时行动力归零，
        /// 部队只能原地攻击/施法，无法移动。系数由 Buff 声明、这里累乘，不写死状态判断。
        /// </summary>
        public int GetTargetMoveAbility(Cell cell)
        {
            int ability = cell.IsWater ? waterMoveAbility : landMoveAbility;
            return ability * buffManager.GetTroopMoveCoef() / 100;
        }

        public bool IsAlliance(BuildingBase other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(BuildingBase other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(BuildingBase other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }
        public bool IsSameForce(Person other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        public bool IsAlliance(Troop other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(Troop other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(Troop other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        // 【重要】下面这组"兵种判定"必须对 LandTroopType 为 null 兜底，不能直接解引用。
        //
        // LandTroopType 是**按需解析**的可空引用（见上面的属性：靠 LandTroopTypeId 经 IdRef.Resolve
        // 反查，id <= 0 或查不到兵种就返回 null）。而这组属性被 AI、资源调度（ResourceBalance）、
        // 技能判定等大量外部代码当作"必定非空"用 —— 只要有一支部队的兵种数据异常，
        // 就会把整回合的流程一起打崩（原先是 ResourceBalance.CollectInbound 里的 NullReferenceException）。
        // 统一兜底成 false，把"一支部队的数据问题"限制在该部队自身。
        /// <summary>是否剑兵（物理孱弱，AI 技能判定时倾向于释放计略）</summary>
        public bool IsSword => LandTroopType != null && LandTroopType.IsSword();
        public bool IsTransport => LandTroopType != null && LandTroopType.IsTransport();
        public bool IsMachine => LandTroopType != null && LandTroopType.IsMachine();
        public bool IsHelepolis => LandTroopType != null && LandTroopType.IsHelepolis();
        public bool IsFight => LandTroopType != null && LandTroopType.isFight;
        public bool IsRange => LandTroopType != null && LandTroopType.isRange;

        /// <summary>
        /// 发起单挑
        /// </summary>
        /// <param name="targetTroop">目标部队</param>
        /// <param name="withView">是否带表现层（false 时瞬时结算）</param>
        /// <returns>是否成功发起单挑</returns>
        public bool StartDuel(Troop targetTroop, bool withView = true)
        {
            if (targetTroop == null || !IsEnemy(targetTroop))
            {
                return false;
            }

            return Duel.DuelManager.Instance.StartDuel(this, targetTroop, withView);
        }

        /// <summary>
        /// 反击系数（百分比）。逐条对齐原版反击判定
        /// （D:\open311\s11\s11_sys_anim_unit.cpp:750-760 与 s11_sys_damage.cpp:501-504）。
        ///
        ///   · 器械不反击（原版 `is_siege_equipment` 直接排除）
        ///   · 处于异常状态不反击（原版 `state_ != Normal`）—— 本项目的"控制 Buff"等价物
        ///   · 计略不触发反击
        ///   · 只有近身相邻才反击；远程距离 > 1 不反击
        ///   · **普通攻击反击 ×0.9，战法反击 ×0.4**（原版两个不同常量）
        ///   · **被包围惩罚**：每多一支相邻敌军，反击再 ×(100 - 5×包围数)%
        /// </summary>
        public int GetAttackBackFactor(SkillInstance skill, int distance)
        {
            if (IsMachine)
                return 0;

            // 原版 state_ != Normal 不反击；本项目以"控制类 Buff"作为等价判定
            if (HasControlBuff())
                return 0;

            if (skill != null && skill.IsStrategy())
                return 0;

            if (distance > 1)
                return 0;

            // 战法反击 0.4，普攻反击 0.9（原版 :501-504）
            int factor = (skill != null && !skill.IsNormal()) ? 40 : 90;

            // 包围惩罚（原版 :759-760）
            int surrounded = CountAdjacentEnemies();
            if (surrounded > 1)
                factor = factor * (100 - 5 * surrounded) / 100;

            return factor;
        }

        /// <summary>本部队相邻格内的敌方部队数（原版 surrounded）</summary>
        int CountAdjacentEnemies()
        {
            if (cell == null) return 0;

            int count = 0;
            for (int i = 0; i < 6; i++)
            {
                Cell neighbor = cell.GetNeighbor(i);
                if (neighbor != null && neighbor.troop != null && neighbor.troop.IsEnemy(this))
                    count++;
            }
            return count;
        }

        public void AttackBuilding(Cell buildingInCell)
        {

        }

        public void AttackBuilding(BuildingBase buildingBase)
        {

        }


        //@param attacker Troops
        //@param defender Troops
        //@param skill Skill
        /// <summary>
        /// 部队打部队的兵力伤害。
        ///
        /// 具体算法已抽到可插拔的 <see cref="TroopDamageMethod"/>（默认 Open311TroopDamageMethod，
        /// 对齐原版 calc_attack_damage → calc_troops_damage），本方法只负责装配上下文：
        ///   · 取当前剧本参数 ScenarioVariables（公式里的基准值都从它读）；
        ///   · 预先算好难度系数（原版 s11_sys_damage.cpp:470-471 只对玩家生效）；
        ///   · 按配置名 troopDamageMethod 取公式实例。
        /// 配置写错或旧存档缺字段时，公式入口会自动回落到默认实现，不会返回 null。
        /// </summary>
        public static int CalculateSkillDamage(Troop attacker, Troop target, SkillInstance skill)
        {
            if (attacker == null || target == null) return 0;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Variables == null) return 0;
            ScenarioVariables vars = scenario.Variables;

            TroopDamageContext ctx = new TroopDamageContext(
                attacker, target, skill, vars,
                CombatFormulaMath.GetDifficultyFactor(attacker.BelongForce, vars));

            return TroopDamageMethod.Create(vars.troopDamageMethod).Calculate(ctx);
        }

        /// <summary>
        /// 部队打建筑的**耐久**伤害。
        ///
        /// 算法已抽到 <see cref="DurabilityDamageMethod"/>（默认 Open311DurabilityDamageMethod，
        /// 对齐原版 calc_hp_damage / calc_shousha_hp_damage，s11_sys_damage.cpp:542-628、1531-1553）。
        /// 本方法只装配上下文并按配置 durabilityDamageMethod 取公式实例。
        /// </summary>
        public static int CalculateSkillDamage(Troop attacker, BuildingBase target, SkillInstance skill)
        {
            if (attacker == null || target == null) return 0;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Variables == null) return 0;
            ScenarioVariables vars = scenario.Variables;

            DurabilityDamageContext ctx = new DurabilityDamageContext(
                attacker, target, skill, vars,
                CombatFormulaMath.GetDifficultyFactor(attacker.BelongForce, vars));

            return DurabilityDamageMethod.Create(vars.durabilityDamageMethod).Calculate(ctx);
        }

        /// <summary>
        /// 部队打据点（城 / 关 / 港）的**守军兵力**伤害。
        ///
        /// 算法已抽到 <see cref="GarrisonDamageMethod"/>（默认 Open311GarrisonDamageMethod，
        /// 对齐原版 calc_attack_damage 的建筑分支，s11_sys_damage.cpp:438-467）：
        ///     井阑型  → calc_troops_damage(完整兵力) × buildingTroopDamageFactor%
        ///     其他    → calc_troops_damage(兵力 × 75%) × 城0.55 / 关0.45 / 港0.60
        /// 本方法只装配上下文并按配置 garrisonDamageMethod 取公式实例。
        ///
        /// 【未对齐】原版在算建筑防御时会把 troops_atk 覆盖成 10（get_ability，1384-1402），
        /// 即战法对建筑不吃兵力攻击加成；本项目暂不覆盖，记为差异项。
        /// </summary>
        public static int CalculateSkillDamageTroopOnCity(Troop attacker, City target, SkillInstance skill)
        {
            if (attacker == null || target == null) return 0;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Variables == null) return 0;
            ScenarioVariables vars = scenario.Variables;

            GarrisonDamageContext ctx = new GarrisonDamageContext(
                attacker, target, skill, vars,
                CombatFormulaMath.GetDifficultyFactor(attacker.BelongForce, vars));

            return GarrisonDamageMethod.Create(vars.garrisonDamageMethod).Calculate(ctx);
        }


        public static int CalculateSkillDamage(BuildingBase attacker, Troop target, SkillInstance skill)
        {

            ScenarioVariables Variables = Scenario.Cur.Variables;

            //基础伤害
            float base_atk = attacker.GetAttack();
            int base_troops = attacker.GetSkillMethodAvaliabledTroops();

            float difficultyDamageFactor = 1;
            if (attacker.BelongForce != null && attacker.BelongForce.IsPlayer)
                difficultyDamageFactor = Variables.DifficultyDamageFactor;

            int damage = (int)(
                 (

                 (System.Math.Max(0, (int)((System.Math.Pow(base_atk, 2) - System.Math.Pow(System.Math.Max(40, target.Defence), 2)) / 300)) +
                 System.Math.Max(0, (base_troops - target.troops) / Variables.fight_base_troops_need) + 50)

                 * 10 * ((int)(

                 (((int)(base_troops * 0.01) + 300) * System.Math.Pow((base_atk + 50), 2)) /
                 (((int)(base_troops * 0.01) + 300) * System.Math.Pow((base_atk + 50), 2) * 0.01 +
                 ((int)(target.troops * 0.01) + 300) * System.Math.Pow((target.Defence + 50), 2) * 0.01)

                 - 50)

                 + 50)

                 * System.Math.Min(System.Math.Pow(System.Math.Max(1, base_troops / 4), 0.5), 40)

                 * Variables.fight_damage_magic_number /* * 太鼓台系数*/

                 + base_troops / Variables.fight_base_troop_count

                 )

                // 难度系数,仅对玩家生效
                * difficultyDamageFactor

                 // 额外增益 (科技系数等)
                 //* System.Math.Max(0, (1 + attacker.DamageTroopExtraFactor))
                 /* * 难度系数*/);

            return damage;
        }

        /// <summary>
        /// 建筑打部队的兵力伤害。
        ///
        /// 已改为逐行对齐原版 calc_troops_damage(Building*, Unit*)
        /// （D:\open311\s11\s11_sys_damage.cpp:1442-1482），实现见
        /// <see cref="CombatFormulaMath.CalcBuildingTroopsDamage"/>。
        ///
        /// 【保留 atk 参数】旧签名把建筑攻击力传进来，但原版这条公式**不读外部 atk**：
        /// 强度完全由设施类型决定（BuildingType.troopAtk）。参数保留只为不改调用方。
        /// </summary>
        public static int CalculateSkillDamage(BuildingBase attacker, Troop target, int atk)
        {
            return CombatFormulaMath.CalcBuildingTroopsDamage(attacker, target);
        }

        // 【已删除】两个 CalculateSkillCriticalBoost 重载（恒返回 false 的死代码）。
        //
        // 暴击的实际链路在 SkillInstance.CalculateCriticalFactor：
        //     skillCriticalMethod.Calculate(...) → 暴击率
        //     GameRandom.Chance(暴击率) → criticalFactor = Variables.skillCriticalFactor
        // 每个战法在 Skills.json 里配 criticalMethod（CommonMethod / FireCriticalMethod / ...），
        // 与 "goal: 单一职责 + 可插拔" 的约定一致。上面这两个方法全库无调用点。

        // 克制系数
        public static float CalculateRestrainBoost(Troop attacker, Troop target)
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            var attack_troops_type = attacker.TroopType;
            float[] t_map = Variables.troops_type_restraint[attack_troops_type.kind];
            var defender_troops_type = target.TroopType;
            return t_map[defender_troops_type.kind];
        }

        public static int CheckTroopTypeLevel(TroopType troopType, Person person)
        {
            int influenceAbility = troopType.influenceAbility - 1;
            if (influenceAbility < 0) return 0;
            switch (troopType.influenceAbility)
            {
                case (int)AbilityType.Spear:
                    return person.SpearLv;
                case (int)AbilityType.Halberd:
                    return person.HalberdLv;
                case (int)AbilityType.Water:
                    return person.WaterLv;
                case (int)AbilityType.Crossbow:
                    return person.CrossbowLv;
                case (int)AbilityType.Ride:
                    return person.RideLv;
                case (int)AbilityType.Machine:
                    return person.MachineLv;
            }
            return 0;
        }


        // 适应力加成
        // 加成值来自兵种适应等级配置(AbilityLevelTypes.json 的 troopsAdaptationLevelBoost 字段)，
        // 等级下标越界时由 ScenarioCommonData.GetAbilityLevelType 回退到最后一个等级。
        // 原版出处: s11_scen_obj_unit.cpp:995 `tekisei_coef = (tekisei + 7) * 0.1f`(线性硬编码, 仅 C/B/A/S 四级)；
        // 本项目扩展到 8 级且以 A=100 为基准，故改为可配置表，数值口径与原 troops_adaptation_level_boost 一致。
        //@param attacker Troops
        public int TroopsLevelBoost(int value, int troopTypeLv)
        {
            Scenario scenario = Scenario.Cur;
            AbilityLevelType levelType = scenario != null && scenario.CommonData != null
                ? scenario.CommonData.GetAbilityLevelType(troopTypeLv)
                : null;

            // 配置缺失(未进剧本或通用数据未加载)时按 100% 处理，避免把部队攻防算成 0
            int boost = levelType != null ? levelType.troopsAdaptationLevelBoost : 100;
            return value * boost / 100;
        }

        internal static List<Cell> tempCellList = new List<Cell>(256);
        internal static List<TroopMoveEvent> tempMoveEventList = new List<TroopMoveEvent>(32);
        internal static List<Cell> spellRangeCells = new List<Cell>(256);
        internal bool isMoving = false;
        internal IRenderEventBase moveRenderEvent = null;
        internal IRenderEventBase actionRenderEvent = null;
        internal IRenderEventBase skillRenderEvent = null;

        /// <summary>
        /// 【新出兵】本部队是刚由据点组建、还没走出城头的"新兵"。
        ///
        /// 生命周期：
        ///   · 组建出城时由 <see cref="CityTroopFactory.DriveOutOfCity"/> 置位；
        ///   · 第一次落到野外格（<see cref="UpdateCell"/> 的 isEndMove）时清除；
        ///   · 部队收尾（阵亡 / 入城解散）时清除。
        ///
        /// 用途：<see cref="CorpsAI.AITroops"/> 排序时把新兵排到最前，让它们**先离开城头**，
        /// 再驱动其它部队。这样同一据点本轮派出的部队不会都挤在城池中心格上
        /// （<see cref="City.EnsureTroop"/> 会无条件覆盖 <c>CenterCell.troop</c>）。
        /// 出城的移动动画照常播放，不走任何同步瞬移。
        /// </summary>
        internal bool isNewEmitted = false;

        /// <summary>
        /// skillRenderEvent 是否由"援助补刀"排入。
        /// 援助不属于这支部队自己的行动, 它播完后不能拿来当部队下一次施法的"已完成"信号,
        /// 否则那次普通攻击会被 SpellSkill 开头的早退分支整个吞掉(没出手却直接结束行动)
        /// </summary>
        internal bool skillRenderEventIsAssist;

        /// <summary>
        /// 该方法必须确定destCell在移动范围内
        /// </summary>
        /// <param name="destCell"></param>
        /// <returns></returns>
        public bool MoveTo(Cell destCell)
        {
            if (destCell == cell)
            {
                moveRenderEvent = null;
                isMoving = false;
                return true;
            }

            if (moveRenderEvent != null && moveRenderEvent.IsDone)
            {
                moveRenderEvent = null;
                isMoving = false;
                return true;
            }
            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append($"{isMoving},");
            if (!isMoving)
            {
                tempCellList.Clear();
                tempMoveEventList.Clear();
                //TODO: 移动
                Scenario.Cur.Map.GetMovePath(this, destCell, tempCellList);

                isMoving = true;
                Cell start = cell;
                for (int i = 1; i < tempCellList.Count; i++)
                {
                    bool isLast = (i == (tempCellList.Count - 1));
                    Cell dest = tempCellList[i];
                    TroopMoveEvent @event = RenderEvent.Instance.Create<TroopMoveEvent>();
                    @event.Init(this, start, dest, isLast, null);

                    if (isLast)
                        moveRenderEvent = @event;

                    RenderEvent.Instance.Add(@event);
                    start = dest;
                }

                if (moveRenderEvent == null)
                {
                    isMoving = false;
                    return true;
                }
            }


            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append($"11{moveRenderEvent},");

            if (moveRenderEvent == null)
            {
                isMoving = false;
                return true;
            }

            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append("11,");

            return false;
        }

        public bool TryMoveToSpell(Cell destCell, SkillInstance skill)
        {
            if (!isMoving)
            {
                tempCellList.Clear();
                tempMoveEventList.Clear();
                spellRangeCells.Clear();
                //TODO: 移动
                Scenario.Cur.Map.GetMovePath(this, destCell, tempCellList);

                isMoving = true;
                Cell start = cell;
                for (int i = 1; i < tempCellList.Count; i++)
                {
                    Cell dest = tempCellList[i];

                    bool findSpell = false;
                    skill.GetSpellRange(this, cell, spellRangeCells);
                    for (int k = 0; k < spellRangeCells.Count; k++)
                    {
                        Cell spellCell = spellRangeCells[k];
                        if (spellCell == destCell)
                        {
                            findSpell = true;
                            break;
                        }
                    }

                    TroopMoveEvent @event = RenderEvent.Instance.Create<TroopMoveEvent>();
                    @event.Init(this, start, dest, i == tempCellList.Count - 1, null);
                    RenderEvent.Instance.Add(@event);
                    start = dest;

                    if (findSpell)
                    {
                        break;
                    }
                }
            }

            return false;
        }


        public bool ChangeGold(int num, bool showInfo = true)
        {
            if (num != 0)
            {
                Render?.ShowInfo(num, (int)InfoType.Gold);
            }

            gold += num;
            if (gold < 0)
            {
                gold = 0;
                return true;
            }
            return false;
        }

        public bool ChangeFood(int num, bool showInfo = true)
        {
            if (showInfo && num != 0)
            {
                Render?.ShowInfo(num, (int)InfoType.Food);
            }

            food += num;
            if (food < 0)
            {
                food = 0;
                return true;
            }
            return false;
        }

        public bool ChangeTroops(int num, SangoObject atk, int atkBack)
        {
            if (!IsAlive)
                return false;

            // 【新增】记录攻击本势力部队的敌方部队,供 AI 主动驱逐
            // 【统一解析】普攻 / 战法都经 Skill 结算，atk 大多是 SkillInstance，
            // 只判 `atk is Troop` 会让绝大多数攻击漏记威胁部队（AI 因此不派人驱逐）。
            Troop atkTroop = atk?.AsTroop();
            if (num < 0 && atkTroop != null && atkTroop.IsAlive && !atkTroop.IsSameForce(this))
            {
                BelongForce?.MarkThreatTroop(atkTroop);
            }

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(num);
            GameEvent.OnTroopChangeTroops?.Invoke(this, atk, atkBack, overrideData);
            num = overrideData.ValueAndRecycle;

            if (num == 0)
                return IsAlive;

            if (Render != null)
            {
                bool isCrit = false;
                if (atk != null && atk.ObjectType == SangoObjectType.SkillInstance)
                {
                    SkillInstance skill = (SkillInstance)atk;
                    if (skill != null)
                    {
                        isCrit = skill.IsCritical();
                    }
                    Render.ShowInfo(num, (int)InfoType.Troop, isCrit);
                }
            }
            troops = troops + num;
            if (num < 0)
            {

                int absNum = System.Math.Abs(num);
                woundedTroops += (int)System.Math.Ceiling(absNum * 0.14f);
                int _foodCost = (int)System.Math.Ceiling(Scenario.Cur.Variables.baseFoodCostInTroop * absNum * TroopType.foodCostFactor) / 2;
                int divFood = 0;
                // 有概率保留部分
                if (GameRandom.Chance(80))
                    divFood += _foodCost;
                if (GameRandom.Chance(50))
                    divFood += _foodCost;
                ChangeFood(-divFood, false);

                IsAlive = troops > 0;

                if (!IsAlive && Render != null && Render.IsVisible())
                {
                    GameMedia.Instance.PlayPersonSay(Leader, GameRandom.Chance(50) ? 3216 : 3230);
                    GameParticales.Instance.PlayEfect("Assets/Effect/Prefab/ef_troop_destroy.prefab", Render.MapObject.position, 3);
                }
            }
            else
            {
                if (troops > MaxTroops)
                    troops = MaxTroops;
            }

            if (!IsAlive)
            {
                // 【关系值：部队被击破】压到 15 地板
                // 对齐 s11_sys_scen_unit.cpp:428-443（与城池陷落、设施摧毁同构）。
                // 这里与 `Clear()` 的区别：`Clear()` 也用于"进城解散 / 正常返城"，
                // 不能在那里判断；而 `ChangeTroops` 的兵力归零分支只由战斗触发。
                // `SangoObject` 基类没有 BelongForce，统一按 atk.AsTroop() 归一：
                //   · 部队攻击 → 自身；战法伤害 → SkillInstance.master（施法部队）
                Force atkForce = atkTroop != null ? atkTroop.BelongForce : null;
                if (atkForce != null)
                    DiplomacyRules.ApplyHostilityPenalty(atkForce, BelongForce);

                Sango.Log.Info($"{BelongForce.Name}的[{Name} 部队 溃灭!!");

                if (Render != null && Render.IsVisible())
                {
                    GameMedia.Instance.PlaySfx(83);
                }

                OnDestroy(atk, atkBack);

                // 移除
                Clear();
            }

            if (Render != null)
            {
                Render.UpdateRender();
            }

            return IsAlive;
        }

        public void ChangeMorale(int num, bool showInfo = true)
        {
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(num);
            GameEvent.OnTroopChangeMorale?.Invoke(this, morale, overrideData);
            num = overrideData.ValueAndRecycle;

            if (num == 0)
                return;

            morale += num;

            if (morale < 0)
                morale = 0;
            else if (morale > MaxMorale)
                morale = MaxMorale;

            if (showInfo)
                Render?.ShowInfo(num, (int)InfoType.Morale);
            Render?.UpdateRender();
        }

        public int GetCaptureChangce()
        {
            return captiveChangce;
        }

        /// <summary>战法命中后触发单挑的概率(百分比)</summary>
        public int GetDuelChance()
        {
            return duelChance;
        }



        public void OnDestroy(SangoObject atk, int atkBack)
        {
            // 添加俘虏流程
            if (atk != null && atk.ObjectType == SangoObjectType.SkillInstance)
            {
                SkillInstance skill = (SkillInstance)atk;

                Troop atkTroop = skill.master;

                if (IsEnemy(atkTroop))
                {
                    List<Person> captives = new List<Person>();
                    // 【原版对齐】临时武将（异族 / 贼，type = Temporary）不参与俘虏判定：
                    // 原版 unit_disband 对 is_temporary() 直接 person_die（s11_sys_scen_unit.cpp:231-257），
                    // 不走"被俘"路径。这里若不加排除，异族武将会被当普通武将送进俘虏事件，
                    // 随后 ClearWithBelongCity 又把它彻底删除，出现"已删除对象仍进俘虏事件"的悬空引用。
                    if (Leader != null && !Leader.IsTemporary && Leader.state != (int)PersonStateType.Governor)
                    {
                        int p = Math.Max(0, atkTroop.GetCaptureChangce() - Leader.escapeFactorWhenTroopDestroy);
                        if (GameRandom.Chance(p))
                            captives.Add(Leader);
                    }
                    if (Member1 != null && !Member1.IsTemporary && Member1.state != (int)PersonStateType.Governor)
                    {
                        int p = Math.Max(0, atkTroop.GetCaptureChangce() - Member1.escapeFactorWhenTroopDestroy);
                        if (GameRandom.Chance(p))
                            captives.Add(Member1);
                    }

                    if (Member2 != null && !Member2.IsTemporary && Member2.state != (int)PersonStateType.Governor)
                    {
                        int p = Math.Max(0, atkTroop.GetCaptureChangce() - Member2.escapeFactorWhenTroopDestroy);
                        if (GameRandom.Chance(p))
                            captives.Add(Member2);
                    }

                    if (captives.Count > 0)
                    {
                        CityRecruitPersonWhenTroopFallEvent te = RenderEvent.Instance.Create<CityRecruitPersonWhenTroopFallEvent>();
                        te.Init(captives, atkTroop);
                        RenderEvent.Instance.Add(te);
                    }
                }
            }

            GameEvent.OnTroopDestroyed?.Invoke(this, atk, atkBack, Scenario.Cur);

        }

        public void ReleaseCaptive()
        {
            // 必须倒序,因为Escape会修改captiveList
            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                person.Escape(EscapeType.TroopDestroyed);
            }
            captiveList.Clear();
        }

        public int GetTroopsNum()
        {
            return troops;
        }

        public bool SpellSkill(SkillInstance skill, Cell spellCell, bool waitForCheer = true)
        {
            // "兄弟同心"加气走异步对白: 本次攻击想吃的这口气力还没真正加满时先挂起,
            // 等对白播完(回调里清标记)再真正出手, 从而保证"先提气、再攻击"。
            // 援助补刀是别人正在结算的攻击的连带演出, 不能被挂起, 故 waitForCheer 传 false。
            if (waitForCheer && pendingSwornCheer)
                return false;

            if (skillRenderEvent != null)
            {
                if (skillRenderEvent.IsDone)
                {
                    skillRenderEvent = null;
                    // 援助补刀留下的"已播完"事件不是本次施法的完成信号, 这里return true会让
                    // 这支部队明明一枪没出, 却被调用方当成"攻击已完成"而结束整个行动
                    if (!skillRenderEventIsAssist)
                        return true;
                    skillRenderEventIsAssist = false;
                }
                else
                    return false;
            }
            // 确认要排一次新的施法事件了, 抹掉技能实例上可能残留的援助标记,
            // 否则这次正常攻击会被当成援助折算伤害, 也不会去呼叫别人援助
            if (skill != null)
            {
                skill.assistAttackFlag = false;
                skill.ResetStrategyTechniquePointFlag();
                skillRenderEventIsAssist = false;
            }
            skill.tempCriticalFactor = 100;
            if (skill.CheckSuccess(spellCell))
            {

                int criticalFactor = skill.CheckCritical(spellCell);
                if (criticalFactor > 100 && !skill.IsNormal())
                {
                    Sango.Log.Info($"{BelongForce.Name}的[{Name} 部队 技能: {skill.Name} =>({spellCell.x},{spellCell.y})]  暴击判定成功!  暴击伤害倍率{criticalFactor}!!");
                    TroopSpellSkillCriticalEvent @event = RenderEvent.Instance.Create<TroopSpellSkillCriticalEvent>();
                    @event.Init(skill, spellCell, criticalFactor);
                    skillRenderEvent = @event;
                    RenderEvent.Instance.Add(@event);
                }
                else
                {
                    TroopSpellSkillEvent @event = RenderEvent.Instance.Create<TroopSpellSkillEvent>();
                    @event.Init(skill, spellCell);
                    skillRenderEvent = @event;
                    RenderEvent.Instance.Add(@event);
                }
            }
            else
            {
                Sango.Log.Info($"{BelongForce.Name}的[{Name} 部队 技能: {skill.Name} =>({spellCell.x},{spellCell.y})]  判定失败! 释放不成功!!");
                TroopSpellSkillFailEvent @event = RenderEvent.Instance.Create<TroopSpellSkillFailEvent>();
                @event.Init(this, skill, spellCell);
                skillRenderEvent = @event;
                RenderEvent.Instance.Add(@event);
            }
            return false;
        }

        public void SupplyTroop(Troop target)
        {

        }

        public void SupplyTroop(Troop target, ItemStore itemStore, int gold, int food, int troops)
        {
            // 【修复】补给方必须保留最低 1 兵，不允许把自己掏空。
            // 本函数直接改 troops（没有走 ChangeTroops），兵力归零不会触发正常收尾
            // （IsAlive 不会更新、不会 Clear），会留下"0 兵但仍存活"的幽灵部队，
            // 继续跑 AI 后在 EnterCity 处以零为分母崩溃。
            if (troops >= this.troops)
                troops = this.troops - 1;
            if (troops < 0)
                troops = 0;

            // 【对齐原版 supply_anim】补给只转移 兵力 / 资金 / 军粮（以及一兵一装兵种的装备），
            // 原版不参与士气计算（s11_sys_anim_unit.cpp:1443-1476 只改 troops/gold/food/energy），
            // 因此这里不再做"士气加权平均"。
            this.troops -= troops;
            this.gold -= gold;
            this.food -= food;
            this.itemStore.Remove(itemStore);
            target.itemStore.Add(itemStore);
            target.troops += troops;
            target.gold += gold;
            target.food += food;

            if (troops > 0)
                Render?.ShowInfo(-troops, (int)InfoType.Troop);
            if (gold > 0)
                Render?.ShowInfo(-gold, (int)InfoType.Gold);
            if (food > 0)
                Render?.ShowInfo(-food, (int)InfoType.Food);

            if (troops > 0)
                target.Render?.ShowInfo(troops, (int)InfoType.Troop);
            if (gold > 0)
                target.Render?.ShowInfo(gold, (int)InfoType.Gold);
            if (food > 0)
                target.Render?.ShowInfo(food, (int)InfoType.Food);

            ActionOver = true;
            // 【修复】补给方自身的名字板需要刷新
            Render?.UpdateRender();
            // 【修复】目标部队的兵力/兵粮/士气同样被本函数直接修改，
            // 但名字板（UITroopHeadbar）没有订阅"数值变化"事件，只有显式调用
            // UpdateRender 才会刷新，因此必须同时刷新目标部队，否则补给后
            // 目标部队的名字板会一直显示旧数值直到下一次回合开始。
            target.Render?.UpdateRender();
        }

        public bool BuildBuilding(Cell dest, BuildingType buildingType)
        {
            if (actionRenderEvent != null)
            {
                if (actionRenderEvent.IsDone)
                {
                    actionRenderEvent = null;
                    return true;
                }
                else
                    return false;
            }

            TroopBuildBuildingEvent @event = RenderEvent.Instance.Create<TroopBuildBuildingEvent>();
            @event.Init(this, buildingType, dest);
            actionRenderEvent = @event;
            RenderEvent.Instance.Add(@event);

            return false;
        }

        Cell tryToDest;
        /// <summary>寻路用的最近可停留格队列(AI 复用,避免每次分配)</summary>
        PriorityQueue<Cell> nearestCellQueue = new PriorityQueue<Cell>();
        public bool TryCloseTo(Cell destCell)
        {
            return TryMoveToCell(destCell);
        }
        public bool TryMoveToCity(City city)
        {
            // 【空引用修复】目标城市或其中心格为空时（如城市被攻陷/数据异常的存档），
            // 无法寻路逃回，直接返回 false，由调用方（TroopEscapeToCityEvent）结束事件。
            if (city == null || city.CenterCell == null)
                return false;
            return TryMoveToCell(city.CenterCell, (x) =>
            {
                return x.building == city;
            });
        }
        public bool TryMoveToCell(Cell targetCell)
        {
            return TryMoveToCell(targetCell, null);
        }

        public bool TryMoveToCell(Cell targetCell, Func<Cell, bool> check)
        {
            if (targetCell == cell)
            {
                moveRenderEvent = null;
                isMoving = false;
                return true;
            }

            if (!isMoving)
            {      //TODO: 尝试移动
                tempCellList.Clear();
                tryToDest = null;

                // 先检查移动范围内是否可达目标
                Map map = Scenario.Cur.Map;
                if (MoveRange.Count == 0)
                    map.GetMoveRange(this, MoveRange);
                for (int i = 1; i < MoveRange.Count; ++i)
                {
                    Cell dst = MoveRange[i];
                    if (dst == targetCell || (check != null && check(dst)))
                    {
                        tryToDest = dst;
                        break;
                    }
                }

                if (tryToDest == null)
                {
                    //TODO: 移动
                    map.GetMinCostDirectPath(this, targetCell, tempCellList);
                    for (int i = tempCellList.Count - 1; i >= 0; i--)
                    {
                        Cell dest = tempCellList[i];
                        if (MoveRange.Contains(dest) )
                        {
                            tryToDest = dest;
                            break;
                        }
                    }
                }

                if (tryToDest != null && (check == null || !check(tryToDest)) && !tryToDest.CanStay(this))
                {
                    // 【性能优化】复用队列实例,避免每次分配 PriorityQueue
                    nearestCellQueue.Clear();
                    nearestCellQueue.reverse = false;
                    for (int i = 0; i < MoveRange.Count; i++)
                    {
                        Cell cell = MoveRange[i];
                        if (cell.IsEmpty() && cell.CanStay(this))
                        {
                            nearestCellQueue.Push(cell, map.Distance(cell, tryToDest));
                        }
                    }
                    tryToDest = nearestCellQueue.Lower();
                }
            }

            if (tryToDest == null)
            {
                return true;
            }

            return MoveTo(tryToDest);
        }

        void UpdateTerrainBonus(Cell destCell)
        {
            // 地形影响
            int terrainId = destCell.TerrainType.Id;
            if (TroopType.terrainIncreaseAtkBonus != null && terrainId < TroopType.terrainIncreaseAtkBonus.Length)
                trrainBonusAtk = TroopType.terrainIncreaseAtkBonus[terrainId];
            else
                trrainBonusAtk = 0;

            if (TroopType.terrainDecreaseDefenceBonus != null && terrainId < TroopType.terrainDecreaseDefenceBonus.Length)
                trrainBonusDef = TroopType.terrainDecreaseDefenceBonus[terrainId];
            else
                trrainBonusDef = 0;
        }

        /// <summary>
        /// 【新出兵】清除"新兵"标记，并解除所在城市的"出城进行中"登记。
        ///
        /// 三个调用点：
        ///   · <see cref="UpdateCell"/> 的 isEndMove —— 正常出城成功；
        ///   · <see cref="Troop.DoAI"/> 的收尾 —— 本回合结束仍未出城（被围死）的兜底；
        ///   · <see cref="ClearWithBelongCity"/> —— 部队阵亡 / 入城解散。
        /// </summary>
        /// <param name="homeCity">归属城；传 null 时按 <see cref="BelongCity"/> 实时推导</param>
        void ClearNewEmittedFlag(City homeCity)
        {
            isNewEmitted = false;

            City home = homeCity != null ? homeCity : BelongCity;
            // 出城队列可能同时挂多支，按引用摘掉自己（不是清空整表）
            if (home != null)
                home.pendingOutTroops.Remove(this);
        }

        public void UpdateCell(Cell destCell, Cell lastCell, bool isEndMove)
        {
            //TODO: 地格更新,需要处理一些事件
            if (lastCell != null)
                GameEvent.OnTroopLeaveCell?.Invoke(this, lastCell, destCell);

            GameEvent.OnTroopEnterCell?.Invoke(this, destCell, lastCell);


            Sango.Log.Info($"{BelongForce.Name}的[{Name} 部队 移动=> ({destCell.x},{destCell.y})]");

            if (destCell.fire != null)
                destCell.fire.BurnTroop(this);

            // 地形移动伤害（原版 get_movement_damage：毒泉 / 栈道 / …）
            // 参数全部来自 TerrainType（moveDamageBase / moveDamageRandom / moveImmunityFeature），
            // 代码不认地形 Id，也不写死数值。
            ApplyTerrainMovementDamage(destCell);

            // 【修复 · 阵亡贴图残留 / 幽灵格】火与地形移动伤害可能直接把部队打光：
            // Clear() 已经跑完（Render 已销毁、cell.troop 已置空），
            // 此时若继续执行下面的 destCell.troop = this，格子会指着一支已死部队，
            // 渲染层也会在已销毁的对象上继续刷新。
            if (!IsAlive)
                return;

            Render.UpdateModelByCell(destCell);
            UpdateTerrainBonus(destCell);

            if (isEndMove)
            {
                destCell.troop = this;
                cell.troop = null;
                cell = destCell;

                // 【新出兵】整段移动结束、且已站到野外格 = 真正走出城头了：
                // 清掉"新兵"标记并解除城市的出城登记，本城随后就能再派下一支。
                if (isNewEmitted && destCell.building == null)
                    ClearNewEmittedFlag(null);

                if (Render.MapObject != null)
                {
                    Render.MapObject.position = cell.Position;
                }
                //else
                //{
                //    Sango.Log.Error($"why {Name}->Render.MapObject is null");
                //}

                // 只有整段移动结束才结算, 途中经过的格子不触发
                TryCheerSwornBrother();
            }
        }

        /// <summary>
        /// 地形移动伤害 —— 原版 get_movement_damage（s11_sys_damage.cpp:5-44）的地形部分。
        ///
        /// 原版：
        ///   毒泉  200 + rand(200)，有「解毒」特技免疫
        ///   栈道  100 + rand(200)，有「踏破」特技 / 「南蛮」科技免疫
        /// 着火格由 <c>Cell.fire.BurnTroop</c> 单独处理（不在本方法内）。
        ///
        /// 本方法不带任何地形 Id / 数值：全部读
        /// <see cref="TerrainType.moveDamageBase"/> / <see cref="TerrainType.moveDamageRandom"/> /
        /// <see cref="TerrainType.moveImmunityFeature"/>，新增地形只要配数据。
        /// </summary>
        void ApplyTerrainMovementDamage(Cell destCell)
        {
            if (destCell == null || !IsAlive) return;

            TerrainType terrain = destCell.TerrainType;
            if (terrain == null || terrain.moveDamageBase <= 0) return;

            // 免疫标记位（毒泉 = PathFlag.IgnorePoison / 栈道 = PathFlag.IgnoreCliffroad）。
            // 标记由特技、科技、事件等任一来源置位，这里只读位。
            if (terrain.moveImmunityFlag != 0 && (pathFlags & terrain.moveImmunityFlag) != 0)
                return;

            int damage = terrain.moveDamageBase;
            if (terrain.moveDamageRandom > 0)
                damage += GameRandom.Range(0, terrain.moveDamageRandom);

            if (damage <= 0) return;

            ChangeTroops(-damage, null, 0);
        }

        /// <summary>队内任一武将是否具备指定特技（原版的 Unit 级 has_skill 聚合）</summary>
        public bool HasFeature(int featureId)
        {
            if (featureId <= 0) return false;
            if (Leader != null && Leader.HasFeatrue(featureId)) return true;
            if (Member1 != null && Member1.HasFeatrue(featureId)) return true;
            if (Member2 != null && Member2.HasFeatrue(featureId)) return true;
            return false;
        }

        /// <summary>
        /// 兄弟同心: 部队移动完成后搜索相邻格子的部队,
        /// 把所有"主将与自己同属一个兄弟组"的友军部队全部找出来(不止一队),
        /// 做一次概率检定, 成功则自己与这些兄弟部队的气力一起推满;
        /// 同一组兄弟部队每季度只结算一次
        /// </summary>
        public const int SwornCheerChance = 10;

        /// <summary>
        /// 执行一次"兄弟同心"检查
        /// </summary>
        /// <returns>是否成功推满气力</returns>
        public bool TryCheerSwornBrother()
        {
            if (!IsAlive) return false;
            // 主将不属于任何兄弟组(剧本原设兄弟/仲介结义)时直接跳过
            if (Leader == null || !Leader.HasSwornBrother) return false;

            int season = Scenario.Cur.SeasonStamp;
            // 本季度已经结算过（每季最多一次，避免一季之内反复把气力推满）
            if (swornCheerSeason == season) return false;

            // 先收齐相邻格里全部同组兄弟部队, 不能只找到第一个就算完
            List<Troop> brothers = new List<Troop>();
            for (int i = 0; i < 6; ++i)
            {
                Cell neighbor = cell.GetNeighbor(i);
                Troop other = neighbor == null ? null : neighbor.troop;
                if (CanCheerWith(other, season))
                    brothers.Add(other);
            }

            if (brothers.Count == 0) return false;

            // 在场各队气力本来就都是满的, 不必再走一场对白
            if (IsAllCheerMoraleFull(brothers)) return false;

            // 整组只做一次判定, 过了就全体推满
            if (!GameRandom.Chance(SwornCheerChance)) return false;

            // 先记下季节, 免得对白期间又有兄弟部队靠过来重复排队（本季度内不再结算）
            swornCheerSeason = season;
            for (int i = 0; i < brothers.Count; i++)
                brothers[i].swornCheerSeason = season;

            // 同步先挂起即将发起的攻击, 保证"先提气、再攻击"(加气可能在对白结束后才落地)
            MarkCheerPending(brothers, true);

            // 交给"兄弟同心"渲染事件演出: 只有玩家控制的部队才弹台词对话框,
            // 电脑(AI)不弹框, 在事件里直接按已通过判定的概率把气力推满
            List<GameDialog.TalkData> talks = IsPlayerControl ? BuildCheerTalks(brothers) : null;
            TroopSwornCheerEvent cheerEvent = RenderEvent.Instance.Create<TroopSwornCheerEvent>();
            cheerEvent.Init(this, brothers, talks);
            RenderEvent.Instance.Add(cheerEvent);
            return true;
        }

        /// <summary>
        /// 批量设置/清除自己和相邻兄弟部队的"提气进行中"标记
        /// </summary>
        internal void MarkCheerPending(List<Troop> brothers, bool pending)
        {
            pendingSwornCheer = pending;
            for (int i = 0; i < brothers.Count; i++)
            {
                Troop brother = brothers[i];
                if (brother != null)
                    brother.pendingSwornCheer = pending;
            }
        }

        /// <summary>
        /// 自己与这些兄弟部队的气力是不是都已经满了
        /// </summary>
        bool IsAllCheerMoraleFull(List<Troop> brothers)
        {
            if (morale < MaxMorale) return false;
            for (int i = 0; i < brothers.Count; i++)
            {
                Troop brother = brothers[i];
                if (brother.morale < brother.MaxMorale)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 组织"兄弟同心"的台词: 参与的每支部队里在场武将每人一句, 兄弟组的组头(兄长)先开口
        /// </summary>
        List<GameDialog.TalkData> BuildCheerTalks(List<Troop> brothers)
        {
            List<GameDialog.TalkData> elderTalks = new List<GameDialog.TalkData>();
            List<GameDialog.TalkData> youngerTalks = new List<GameDialog.TalkData>();

            // 自己排最前, 其余兄弟部队按相邻格顺序
            CollectCheerTalks(this, elderTalks, youngerTalks);
            for (int i = 0; i < brothers.Count; i++)
                CollectCheerTalks(brothers[i], elderTalks, youngerTalks);

            // 不用Sort(不稳定排序), 直接分两组拼接, 保证兄长在前、同队内主将先于副将
            List<GameDialog.TalkData> talks = new List<GameDialog.TalkData>(elderTalks.Count + youngerTalks.Count);
            talks.AddRange(elderTalks);
            talks.AddRange(youngerTalks);
            return talks;
        }

        /// <summary>
        /// 按主将、副将的顺序收集一支部队里每个人的台词
        /// </summary>
        static void CollectCheerTalks(Troop troop, List<GameDialog.TalkData> elderTalks, List<GameDialog.TalkData> youngerTalks)
        {
            // 兄弟提气只有主将说话，副将不参与
            Person person = troop.Leader;
            if (person == null) return;
            GameDialog.TalkData talk = new GameDialog.TalkData();
            talk.person = person;
            talk.text = CheerTalk(person);
            if (IsFamilyHead(person))
                elderTalks.Add(talk);
            else
                youngerTalks.Add(talk);
        }

        /// <summary>
        /// 是否兄弟组的组头: 仲介结义与剧本原设都是组头的 BrotherId 指向自己
        /// </summary>
        static bool IsFamilyHead(Person person)
        {
            return person != null && person.BrotherId > 0 && person.BrotherId == person.Id;
        }

        /// <summary>
        /// 兄长与幼弟各自的台词, 各备两句随机, 免得反复触发时千篇一律
        /// </summary>
        static string CheerTalk(Person person)
        {
            return GameRandom.Chance(50)
                ? $"你我兄弟并肩，定能奋力杀敌！"
                : "兄弟同心，定能战胜敌军！";
        }

        /// <summary>
        /// 相邻格子的这支部队算不算"可以一起受激励的兄弟部队"
        /// </summary>
        /// <param name="other">相邻格子上的部队</param>
        /// <param name="season">当前季节编号，本季已结算过的部队不再参与</param>
        bool CanCheerWith(Troop other, int season)
        {
            if (other == null || other == this) return false;
            if (!other.IsAlive || other.Leader == null) return false;
            if (other.swornCheerSeason == season) return false;

            // 友军: 同势力或同盟
            if (!IsSameForce(other) && !IsAlliance(other)) return false;

            // 主将与自己同属一个兄弟组;
            // 必须用对称的 IsBrotherGroupmate: Person.IsBrother 读的是 BrotherList,
            // 而幼弟那份列表里不一定含组头, 会让幼弟移动时漏掉兄长那支部队。
            // 同一个组里的人彼此都是兄弟, 所以相邻的多支部队无需再两两判定
            if (!Leader.IsBrotherGroupmate(other.Leader)) return false;

            return true;
        }

        /// <summary>
        /// 对白全部说完后, 把自己和所有相邻兄弟部队的气力一起推到上限
        /// </summary>
        internal void CheerFullMorale(List<Troop> brothers)
        {
            CheerSelfFullMorale();
            for (int i = 0; i < brothers.Count; i++)
            {
                Troop brother = brothers[i];
                if (brother != null)
                    brother.CheerSelfFullMorale();
            }

            Sango.Log.Info($"{BelongForce.Name}的[{Name}]与兄弟部队[{JoinTroopNames(brothers)}]同心同德，{brothers.Count + 1}队气力推满！");
        }

        /// <summary>
        /// 用顿号拼接兄弟部队名, 仅供日志使用
        /// </summary>
        static string JoinTroopNames(List<Troop> troops)
        {
            string names = "";
            for (int i = 0; i < troops.Count; i++)
            {
                if (troops[i] == null) continue;
                names += names.Length == 0 ? troops[i].Name : "、" + troops[i].Name;
            }
            return names;
        }

        /// <summary>
        /// 把本部队气力推到上限
        /// </summary>
        void CheerSelfFullMorale()
        {
            // 对白期间部队可能已经溃灭或回城, 这里要重新检查
            if (!IsAlive) return;

            int num = MaxMorale - morale;
            if (num > 0)
                ChangeMorale(num);
        }

        /// <summary>
        /// 援助攻击的伤害折算比例(百分比)
        /// </summary>
        public const int AssistAttackDamagePercent = 50;

        /// <summary>
        /// 援助搜索半径(格)。以敌军为圆心向外螺旋收候选:
        /// 一格内只要不是器械就能近战补刀, 两格及以上必须是射程够得着的弓箭军
        /// </summary>
        public const int AssistAttackRange = 4;

        // 螺旋候选格与远程射程格: 用静态缓冲复用, 免得每次呼叫援助都分配 List
        internal static List<Cell> assistSearchCells = new List<Cell>(128);
        internal static List<Cell> assistRangeCells = new List<Cell>(256);

        /// <summary>
        /// 各关系的援助概率档位(百分比)。
        /// 辅佐特技的概率不再用常量: 由 Features.json 的 value 经 TroopAssistAttack 写入 Troop.assistChance
        /// </summary>
        public const int AssistChanceSpouse = 50;
        public const int AssistChanceSwornBrother = 50;
        public const int AssistChanceLike = 30;
        public const int AssistChanceBloodRelative = 20;

        /// <summary>
        /// 临时诊断开关: 置 true 时把援助攻击每一层的拦截原因/概率/命中写进日志(见 LogAssist 的 [Assist] 输出),
        /// 功能正常后保持 false; 以后要排查援助问题把它改回 true 即可, 无需动其他代码。
        /// </summary>
        public static bool AssistDiagnosis = false;

        /// <summary>
        /// 援助攻击: 本部队攻击敌军却没把它打灭时, 敌军周围 AssistAttackRange 格内的友军部队
        /// 按其与本部队主将的关系分档判定概率, 命中则各补一记打折的普攻。
        /// 出手资格按距离分层: 一格内不是器械就能直接近战补刀,
        /// 两格及以上只能是弓箭军(带远程普攻), 而且敌军要落在它的射程里
        /// </summary>
        public void TryAssistAttack(Troop enemy)
        {
            if (!IsAlive || enemy == null || !enemy.IsAlive)
            {
                return;
            }
            if (Leader == null)
            {
                return;
            }
            Cell enemyCell = enemy.cell;
            Map map = Scenario.Cur == null ? null : Scenario.Cur.Map;
            if (enemyCell == null || map == null)
            {
                return;
            }

            // 用 GetSpiral 而不是 SpiralAction: 后者走的 RingAction 在地图边缘会先解引用再判空,
            // 半径大于1时可能空引用; GetSpiral 内部逐格 GetCell 已判过空
            // 螺旋由近及远, 天然让身边的部队优先补刀
            assistSearchCells.Clear();
            map.GetSpiral(enemyCell, AssistAttackRange, assistSearchCells);

            LogAssist($"==== [{this.Name}] 呼叫援助: 候选格={assistSearchCells.Count}");

            int candidates = 0;
            int joined = 0;
            for (int i = 0, count = assistSearchCells.Count; i < count; ++i)
            {
                Cell cell = assistSearchCells[i];
                // 圆心这一格就是敌军自己站着的格, 不是候选援助位
                if (cell == enemyCell) continue;
                Troop helper = cell.troop;
                if (helper == null) continue;
                candidates++;

                int distance = map.Distance(enemyCell, cell);

                string block = AssistBlockReason(helper, enemy);
                if (block != null)
                {
                    LogAssist($"[{helper.Name}] 被拦: {block}");
                    continue;
                }

                // 兵种与射程按距离分层, 顺带挑出这一发要用哪个普攻技能
                string reject = null;
                SkillInstance skill = SelectAssistSkill(helper, cell, enemyCell, distance, out reject);
                if (skill == null)
                {
                    LogAssist($"[{helper.Name}] 出不了手: {reject}");
                    continue;
                }

                int chance = CalcAssistChance(this, helper);
                if (chance <= 0)
                {
                    LogAssist($"[{helper.Name}] 概率=0, 不援助");
                    continue;
                }

                bool hit = GameRandom.Chance(chance);
                LogAssist($"[{helper.Name}] 概率={chance}, {(hit ? "命中→出手" : "未命中")}");
                if (!hit) continue;

                if (DoAssistAttack(helper, enemy, skill))
                    joined++;
            }
            LogAssist($"==== [{this.Name}] 援助结束: 候选{candidates} 实际出手{joined}");
        }

        /// <summary>
        /// 援助攻击的临时诊断输出
        /// </summary>
        public static void LogAssist(string message)
        {
            if (!AssistDiagnosis) return;
            UnityEngine.Debug.Log("[Assist] " + message);
        }

        /// <summary>
        /// 按关系分档计算 helper 前来支援 self 本次攻击的概率(百分比)。
        /// 关系档位: 夫妇50 / 义兄弟50 / 亲爱30 / 直系血亲20(需无厌恶);
        /// 关系档位结果与支援方部队的辅佐概率(helper.assistChance)取Max(不叠加)作为最终概率,
        /// 辅佐率仅在双方没有厌恶时参与取Max。
        /// 效率优化: 最终结果取Max, 任何档位值 ≤ 当前最大值时都不可能改变结果,
        /// 因此昂贵的武将关系判定(配偶/义兄弟/亲爱/血亲)能省则省, 只在可能改变Max时才执行;
        /// 厌恶判定也只在需要它(辅佐率参与比较或血亲档)时才查
        /// </summary>
        public static int CalcAssistChance(Troop self, Troop helper)
        {
            if (self == null || helper == null)
            {
                return 0;
            }

            Person leader = self.Leader;
            Person helperLeader = helper.Leader;
            if (leader == null || helperLeader == null)
            {
                return 0;
            }

            // 支援方(helper)部队的辅佐概率: 辅佐是"持有者主动去助攻邻近友军的攻击",
            // 故取 helper(来支援的那支部队)而非 self; 该值由 TroopAssistAttack 在属性计算阶段写入,
            // 任一武将持有"辅佐"即生效, 多名武将取Max不叠加。
            // 字段读取, 不涉及关系表查找, 先拿它当比较基准
            int chance = helper.assistChance;

            // 厌恶只在两处生效: 拦下辅佐率、拦下血亲档; 都用不到时就不做这次关系表查找
            bool hated = false;
            bool hatedChecked = false;
            if (chance > 0)
            {
                // 辅佐率参与比较前先查厌恶, 查过就用结果, 后面血亲档不必重复查
                hatedChecked = true;
                hated = Person.IsHatedByEither(leader, helperLeader);
                // 辅佐率被厌恶拦下时不再参与取Max, 置0后关系档位照常判定
                if (hated)
                    chance = 0;
            }

            // 关系档位取命中的第一档; 某档的值已 ≤ chance 时, 命中也改变不了Max, 直接跳过该档判定
            if (chance < AssistChanceSpouse)
            {
                // 夫妇50 / 义兄弟50
                if (leader.IsSpouse(helperLeader))
                    chance = AssistChanceSpouse;
                else if (leader.IsBrotherGroupmate(helperLeader))
                    chance = AssistChanceSwornBrother;
                else if (chance < AssistChanceLike && leader.IsLike(helperLeader))
                    // 亲爱30
                    chance = AssistChanceLike;
                else if (chance < AssistChanceBloodRelative && !hated && leader.IsBloodRelative(helperLeader))
                {
                    // 直系血亲20: 该档要求双方没有厌恶; 厌恶若尚未查过(辅佐率为0的路径), 此处补查一次
                    if (!hatedChecked)
                    {
                        hatedChecked = true;
                        hated = Person.IsHatedByEither(leader, helperLeader);
                    }
                    if (!hated)
                        chance = AssistChanceBloodRelative;
                }
            }

            // 诊断输出: 先判开关再拼字符串, 避免热路径上无谓的字符串分配;
            // 为还原完整判定过程, 诊断时才补算被跳过的关系项
            if (AssistDiagnosis)
            {
                bool isSpouse = leader.IsSpouse(helperLeader);
                bool isSwornBrother = leader.IsBrotherGroupmate(helperLeader);
                bool isLike = leader.IsLike(helperLeader);
                bool isBloodRelative = leader.IsBloodRelative(helperLeader);
                int rawAssistChance = helper.assistChance;
                LogAssist($"[Calc] [{helperLeader.Name}] 支援 [{leader.Name}]: 夫妇={isSpouse} 义兄弟={isSwornBrother} 辅佐率(helper)={rawAssistChance} 亲爱={isLike} 血亲={isBloodRelative} 厌恶={hated}");

                if (chance <= 0)
                {
                    if (rawAssistChance > 0 || isBloodRelative)
                        LogAssist("→ 0 (虽有辅佐率或血亲, 但双方存在厌恶关系, 被拦下)");
                    else
                        LogAssist("→ 0 (关系档位与辅佐率全不沾, 改概率常量也不会触发)");
                }
                else
                    LogAssist($"→ {chance}");
            }

            return chance;
        }

        /// <summary>
        /// 这支部队是否具备前来援助的战场条件(不含兵种与射程, 那部分由 SelectAssistSkill 判)
        /// </summary>
        bool CanAssistAttack(Troop helper, Troop enemy)
        {
            return AssistBlockReason(helper, enemy) == null;
        }

        /// <summary>
        /// 战场条件逐条检查; 返回null表示通过, 否则返回被哪一条拦下。
        /// 兵种、射程这类与距离相关的资格由 SelectAssistSkill 判
        /// </summary>
        string AssistBlockReason(Troop helper, Troop enemy)
        {
            if (helper == null) return "该格没有部队";
            // 螺旋把圆心格也收了进来, 那格站的就是敌军自己
            if (helper == enemy) return "就是敌军本队";
            // 攻击者自己再打一次不算援助
            if (helper == this) return "就是攻击方自己";
            if (!helper.IsAlive) return "无法攻击";
            // 路上的部队不参与援助: 它的施法槽位会被移动事件清掉, 半路插手会把表现搞乱
            if (helper.isMoving) return "正在移动";
            // 必须是自己的友军: 同势力或同盟
            if (!IsSameForce(helper) && !IsAlliance(helper)) return "既不同势力也不同盟";
            // 援助方也得真能与敌军交战
            if (!helper.IsEnemy(enemy)) return "与该敌军不构成敌对";
            // 前面的援助已经把敌军打灭, 后面的不必再补刀
            if (!enemy.IsAlive) return "敌军已被打灭";
            if (enemy.cell == null) return "敌军已不在地图上";

            return null;
        }

        /// <summary>
        /// 按援助方与敌军的距离挑出本次补刀要用的普攻, 同时校验兵种与射程是否允许出手。
        /// 一格: 不是器械就行, 用近战普攻;
        /// 两格及以上: 只能用带远程普攻的弓箭/弩兵, 而且敌军必须在它的射程之内。
        /// 返回null表示出不了手, reject 给出原因
        /// </summary>
        SkillInstance SelectAssistSkill(Troop helper, Cell helperCell, Cell enemyCell, int distance, out string reject)
        {
            reject = null;

            // 兵器队一律不援助: 近战不会使, 远程也不给(两格外只留给弓箭军)
            if (helper.IsMachine)
            {
                reject = "是兵器部队";
                return null;
            }

            if (distance <= 1)
            {
                SkillInstance melee = helper.NormalSkill;
                if (melee == null)
                {
                    reject = "没有近战普攻";
                    return null;
                }
                return melee;
            }

            // "是不是弓箭军"以有没有远程普攻为准: 水战时 NormalRangeSkill 会切到水军那一份,
            // 而 LandTroopType.isRange 只反映陆战兵种, 拿它做判据会把水上的弓兵误杀
            SkillInstance ranged = helper.NormalRangeSkill;
            if (ranged == null)
            {
                reject = $"与敌军隔{distance}格, 不是弓箭军(没有远程普攻)";
                return null;
            }

            // 射程校验: GetSpellRange 给出的是这支部队站在原地能施放的格子集合, 敌军得在里面
            assistRangeCells.Clear();
            ranged.GetSpellRange(helper, helperCell, assistRangeCells);
            bool reachable = false;
            for (int i = 0, count = assistRangeCells.Count; i < count; ++i)
            {
                if (assistRangeCells[i] == enemyCell)
                {
                    reachable = true;
                    break;
                }
            }
            if (!reachable)
            {
                reject = $"与敌军隔{distance}格, 超出<{ranged.Name}>的射程";
                return null;
            }

            // 与攻击菜单判断"有没有目标可打"用的是同一套条件, 免得这里放行、真正施法却被拒
            if (!ranged.CanSpellToHere(helper, enemyCell))
            {
                reject = $"<{ranged.Name}>对敌军所在格不满足施放条件";
                return null;
            }

            return ranged;
        }

        /// <summary>
        /// 让支援部队以完全等同于普通攻击的方式出手:
        /// 走 SpellSkill 进入渲染事件队列, 动画/暴击/伤害/敌军还击/EP/气力消耗全部自动生效,
        /// 多支部队依次演出而不是瞬间一起扣血。返回是否真的排上了队列
        /// </summary>
        bool DoAssistAttack(Troop helper, Troop enemy, SkillInstance skill)
        {
            // 与普通攻击同样的释放前置检查(气力不足就不能出手)
            if (!skill.CanBeSpell(helper))
            {
                LogAssist($"[{helper.Name}] 气力不足(需{skill.costEnergy}), 放弃援助");
                return false;
            }

            // SpellSkill 开头遇到"已播完但没清空"的旧事件时, 会只清掉旧事件就 return true,
            // 一个援助事件也不会排, 所以这里必须先把它们清干净
            if (helper.skillRenderEvent != null)
            {
                if (helper.skillRenderEvent.IsDone)
                    helper.skillRenderEvent = null;
                else
                {
                    LogAssist($"[{helper.Name}] 正在施法, 本次援助放弃");
                    return false;
                }
            }

            helper.SpellSkill(skill, enemy.cell, false);

            // SpellSkill 只是把事件排进队列, 伤害要等轮到该事件才结算,
            // 所以援助标记必须排在队之后再挂: 它挂在技能实例上, 结算一次即消耗,
            // 不会像挂在部队上那样残留到该部队自己的下一次攻击
            if (helper.skillRenderEvent == null)
            {
                LogAssist($"[{helper.Name}] 援助未能排入渲染队列, 本次放弃");
                return false;
            }
            skill.assistAttackFlag = true;
            // 这场援助演完就该把这支部队的施法状态让出来, 它自己的行动还没用
            helper.skillRenderEventIsAssist = true;
            // 把事件标成援助: 敌军可能在排队的这几发之间就被打灭了,
            // 那时这场补刀要整场取消(见 TroopSpellSkillEvent.IsAssistCancelled)。
            // 普攻必中也不会进暴击分支, 所以援助排出的只会是 TroopSpellSkillEvent
            if (helper.skillRenderEvent is TroopSpellSkillEvent spellEvent)
                spellEvent.isAssistAttack = true;

            LogAssist($"[{helper.Name}] 已排入援助攻击队列, 用<{skill.Name}>打[{enemy.Name}]");
            return true;
        }

        /// <summary>
        /// 部队入城（解散 / 卸货 / 占领）。
        /// </summary>
        /// <param name="city">目标据点</param>
        /// <param name="checkTroopOverflow">
        /// 是否执行"AI 溢出改道"检查。默认 true。
        /// 该检查**只对港关（港口 / 关隘）生效**，城池一律不检查 ——
        /// 城兵满时还把部队改道，部队就永远回不了城。
        /// 城陷占领、败退入城、编成回滚这几条路径必须传 false ——
        /// 那些场景下改道会让刚打下的城没人守、或让残部在地图上被追杀。
        /// </param>
        public void EnterCity(City city, bool checkTroopOverflow = true)
        {
            if (city == null)
            {
                Sango.Log.Error($"{BelongForce?.ColorName}的{Name}试图进入空引用城市,已拒绝入城!");
                return;
            }

            // 【原版对齐】无主空城可以直接占领。
            // Open311 的 unit_enter 开头就有这条：`if (building->get_force_id() == -1)`
            // 先 building_change_district 把城挂到自己军团，再走正常入城流程。
            // 缺了它，被中立化 / 势力灭亡清空的城会变成"死城"——谁都不能进，
            // 只能等归属巡检或编辑器处理，AI 会一圈圈绕着走。
            if (city.BelongForce == null)
            {
                if (BelongCorps == null)
                {
                    Sango.Log.Error($"{BelongForce?.ColorName}的{Name}试图占领{city.ColorName}，但自己没有军团归属,已拒绝");
                    return;
                }

                Sango.Log.Info($"{BelongForce?.ColorName}的{Name}占领了无主据点{city.ColorName}");
                CityStateSystem.SetCorps(city, BelongCorps);
            }

            // 【硬守卫】其余情况只允许进入己方势力城池：
            // 若存在"进城瞬间目的地易主"的时序窗口（如败退途中城池被第三方攻占），
            // 直接拒绝入城，避免兵力 / 钱粮被并入敌城。部队保持原状留在地图上，
            // 后续由 DoAI 出城兜底 / 待命兜底 / 任务逻辑处理其归宿。
            if (city.BelongForce != BelongForce)
            {
                Sango.Log.Error($"{BelongForce?.ColorName}的{Name}试图进入非自身势力{city.BelongForce?.ColorName}{city.ColorName}的城市,已拒绝入城!");
                return;
            }

            // 【防吞兵 · 仅 AI · 仅港关】目标港关装不下这支部队时，先改道最近的有余量据点。
            //
            // 原版做法是"clamp 后丢弃"：unit_enter 直接调 building_add_troops
            // （Open311 s11_sys_scen_unit.cpp:573），而它把结果 clamp 到 max 并返回实际变化量
            // （s11_sys_scen_building.cpp:1145-1154），调用方不接返回值 —— 溢出兵力就此蒸发。
            // 玩家侧保持该行为（提示放在下令交互阶段，不在这里弹窗）；AI 侧不能自己把兵送没，故在此改任务。
            //
            // 【城池不检查】城池容量大，且满兵是常态：若城兵满也拦着改道，部队就永远回不了城。
            // 城池入城仍按原版 clamp 并入（溢出部分丢弃），保证部队能解散回城。
            if (checkTroopOverflow
                && city.IsPortOrGate()
                && troops > 0
                && !IsPlayerControl
                && TroopAIFallback.GetRoom(city) < troops
                && TroopAIFallback.TryRerouteForOverflow(this, city, Scenario.Cur))
            {
                return;
            }
            // 所属城取主将的实时值（不缓存），下面几处统一用它：
            // 主将已被俘/阵亡时它是 null，这几处都要能安全跳过（?.），否则会在这里空引用崩溃
            City lastBelongCity = BelongCity;

            // 中和士气
            // 【防御】分母可能为 0：本部队兵力已经为 0（补给把兵力全部交出等绕过 ChangeTroops 的
            // 路径会产生"兵力归零但仍存活"的幽灵部队），且城市吸收后仍无兵力（空城 /
            // 兵力上限 TroopsLimit 为 0 的据点）。此时没有可供加权平均的兵力，
            // 保持城市原有士气即可，绝不能强行除以 0。
            int totalTroops = city.troops + troops;
            if (totalTroops > 0)
                city.morale = (city.morale * city.troops + morale * troops) / totalTroops;

            city.AddGold(gold);
            city.AddFood(food);
            // 【城池吞兵留痕】城池不做容量拦截（见上面的港关限定），但 AddTroops 仍按原版 clamp 截断，
            // 溢出部分确实会消失 —— 必须留痕，否则又变成"兵凭空消失"。
            if (city.IsCity())
            {
                int cityRoom = TroopAIFallback.GetRoom(city);
                if (cityRoom < troops)
                {
                    Sango.Log.Warning($"{BelongForce?.ColorName}的[{Name}]进入{city.ColorName}时兵力溢出，"
                                      + $"丢弃 {troops - cityRoom} 兵（城 {city.troops}/{city.TroopsLimit}，部队 {troops}）");
                }
            }
            city.AddTroops(troops);

            // 处理俘虏：随军俘虏就地转押入城。
            // 必须先去重 —— SangoObjectList.Add 遇到重复元素会打错误日志，
            // 而俘虏可能因为以下原因已经在本城的 captiveList 里：
            //   · 本城就是他原来的关押城，部队只是出门转了一圈又回来；
            //   · 同一回合内先 AddCaptive 后又触发入城；
            //   · 历史存档里残留的重复登记。
            // 【性能】原 ForEach(lambda) 捕获 city，改为按下标遍历。
            for (int i = 0; i < captiveList.Count; i++)
            {
                Person p = captiveList[i];
                if (p == null)
                    continue;
                p.mBelongTroop = null;
                if (!city.captiveList.Contains(p))
                    city.captiveList.Add(p);
                p.ChangeCurrentCity(city);
            }
            captiveList.Clear();

            // 返还兵装
            city.itemStore.Gain(LandTroopType.costItems, troops + woundedTroops);
            city.itemStore.Gain(WaterTroopType.costItems, troops + woundedTroops);
            city.woundedTroops += woundedTroops;
            city.itemStore.Add(itemStore);

            ForEachPerson((person) =>
            {
                person.ActionOver = true;
            });

            if (LandTroopType != null && LandTroopType.isFight && LandTroopType.Id != 1)
                lastBelongCity?.allAttackTroops.Remove(this);

            lastBelongCity?.allTroops.Remove(this);
            city.Render.UpdateRender();

            if (city == lastBelongCity)
            {
                Clear();
                Sango.Log.Info($"{BelongForce?.Name}的[{Name}]部队回到{city.BelongForce?.Name}的城池:<{city.Name}>");
                return;
            }

            // 【运输队入城询问】要不要弹"返回原地 / 留在本城"，只取决于"这支部队是不是运输（非战斗）部队"。
            //
            // 判据必须用 LandTroopType，不能用 TroopType：
            //   TroopType = IsInWater ? WaterTroopType : LandTroopType，
            //   而运输队的水上兵种固定是走舸（isFight = true，见 CityTransport.MakeTroop），
            //   于是从水路（港口 / 沿海城）抵达时会被误判成战斗部队，选择框直接消失。
            //
            // 原先还叠了一个 `missionParams1 <= 0` 的门控（注释写着"== 1 是AI运输的"），
            // 但该字段实际被"移动"命令写成目标格坐标（TroopInteractiveMoveToCell.cs:42）、
            // 求援前存旧任务枚举、连建计数也在用，而全库没有任何地方写 1；
            // 只要这支部队本回合先"移动"过一次，该门控就恒为 false，弹窗同样不会出现。
            // AI / 非第一军团部队的静默处理由下面的 IsPlayerControl 分支负责，不需要这个字段。
            bool isTransportTroop = LandTroopType != null && !LandTroopType.isFight;
            if (isTransportTroop)
            {
                if (BelongCorps.IsPlayerControl)
                {
                    List<Person> pList = new List<Person>();
                    ForEachPerson((person) =>
                    {
                        pList.Add(person);

                    });

                    PlayerChoice.ChoiceData[] choiceDatas = new PlayerChoice.ChoiceData[]
                    {
                        new PlayerChoice.ChoiceData()
                        {
                           lab = $"返回{lastBelongCity?.ColorName}",
                           call = () =>
                           {
                               foreach(Person person in pList)
                               {
                                   person.ChangeCurrentCity(city);
                                   person.SetMission(MissionType.PersonReturn, person.BelongCity);
                               }
                               pList.Clear();
                           }
                        },
                        new PlayerChoice.ChoiceData()
                        {
                           lab = $"将其留在{city.ColorName}",
                           call = () =>
                           {
                               foreach(Person person in pList)
                               {
                                   // 【归属治理】留在本城 = 入城，统一走 person_enter 语义
                                   PersonStateSystem.Enter(person, city, PersonChangeReason.Entered);
                               }
                               pList.Clear();
                           }
                        }
                    };
                    //GameSystem.GetSystem<PlayerChoice>().Start(choiceDatas);

                    TroopTransformChoiceEvent troopTransformChoiceEvent = new TroopTransformChoiceEvent()
                    {
                        choiceDatas = choiceDatas
                    };
                    RenderEvent.Instance.Add(troopTransformChoiceEvent);

                }
                else
                {
                    // 运输武将返回所属城市
                    ForEachPerson((person) =>
                    {
                        person.ChangeCurrentCity(city);
                        person.SetMission(MissionType.PersonReturn, person.BelongCity);
                    });
                }
            }
            else
            {
                ForEachPerson((person) =>
                {
                    // 【归属治理】入城 = 入城事件（对应 Open311 unit_enter 里
                    // person_set_location_id + service 改指入城据点 + building_appoint_taishu）。
                    // 统一走 PersonStateSystem.Enter：迁户口、改所在地、维护在籍名册、标记太守重算。
                    // 原先这里手写 ChangeBelongCity + ChangeCurrentCity，漏掉了太守重算 ——
                    // 部队把城里最后一名在籍武将带走再送回来时，城会一直停在"太守为空"。
                    PersonStateSystem.Enter(person, city, PersonChangeReason.Entered);
                });
            }

            // 【渲染兜底】部队若此前已经走过一次收尾（isCleared == true），
            // 下面的 Clear() 会在 ClearWithBelongCity 开头直接 return，
            // 连 Render.Clear() 都不执行 —— 结果是"人已经进城了，地图上还残留一支部队"。
            // 入城是武将归属已经迁走的确定状态，这里无条件再清一次渲染。
            Render?.Clear();
            Clear();

            Sango.Log.Info($"{BelongForce?.Name}的[{Name}]部队进入{city.BelongForce?.Name}的城池:<{city.Name}>");
        }

        public override void Clear()
        {
            ClearWithBelongCity(BelongCity);
        }

        /// <summary>
        /// 部队收尾。belongCity 只用于"把自己从所属城的名单里摘除"：
        ///   · 常规路径传 BelongCity —— 主将实时推导，主将换势力/换城后天然跟着变，不会过期；
        ///   · 武将刚被全部摘除时主将已为空、推导不出来，由调用方在摘除**之前**取好传进来（见 RemovePerson）。
        /// 传 null 时跳过城池名单摘除（不崩），但会留下脏引用，故调用方尽量给准确值。
        /// </summary>
        public void ClearWithBelongCity(City belongCity)
        {
            // 收尾只走一遍：兵力归零(ChangeTroops)与武将全部离开(RemovePerson)都可能触发本函数
            if (isCleared)
                return;
            isCleared = true;

            // 【修复 · 阵亡贴图残留】渲染销毁提前到收尾最前面。
            // 原先排在方法末尾（原 3553 行）：中间任何一步抛异常，isCleared 已经锁死，
            // 之后所有 Clear() 都会从上面的早退返回，MapObject 永远留在 MapRender.dynamicObjects
            // 里逐帧绘制 —— 玩家看到的就是"敌军阵亡后原地残留贴图"。
            // 判空用 ?. ：未 Init 的部队 Render 可能为 null，原写法会直接抛 NRE（同样会跳过后续收尾）。
            Render?.Clear();

            belongCity?.allTroops.Remove(this);
            Scenario.Cur.Remove(this);

            if (Scenario.Cur.IsAlive)
            {
                ReleaseCaptive();
            }
            buildingImproveMap.Clear();
            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();

                actionList.Clear();
                actionList = null;
            }

            if (LandTroopType != null && LandTroopType.isFight && LandTroopType.Id != 1)
                belongCity?.allAttackTroops.Remove(this);


            ForEachPerson((person) =>
            {
                person.mBelongTroop = null;

                // 【原版对齐】临时武将（异族 / 贼的克隆体，type = Temporary）部队被消灭后
                // 直接彻底删除，不下野、也不落回据点。对应 Open311 unit_disband 对
                // is_temporary() 的收集 + 末尾 person_die（s11_sys_scen_unit.cpp:231-233、256-257）。
                // 本项目临时武将是每次出兵从模板深拷贝、scenario.Add 动态分配 Id 的，
                // 彻底删除等价于原版"复用 inactive 槽"；若只标记死亡（Dead），
                // 每次出兵克隆一个、被消灭留一个，死临时武将会无限累积。
                if (person.IsTemporary)
                {
                    Scenario.Cur.Remove(person);
                    return;
                }

                // 【原版对齐】部队消失后武将必须重新有落脚点。
                // Open311 的 unit_disband 会：取最近据点 -> person_set_location_id，
                // 所属城与落点不同则额外 person_leave + Return 命令；
                // 溃灭路径则 person_set_zaiya 转在野。
                // 这里原先只清 mBelongTroop，武将的 CurrentCity 会停在"部队最后经过的城"
                // （可能是敌方城，甚至为空），形成"户口在己方城、人飘在野外"的脏状态。
                //
                // 归一处理：有所属城就落回所属城（入城口径与 person_enter 一致），
                // 没有所属城（势力已灭）则转在野由巡检安置。
                if (person.IsPrisoner)
                    return;

                City home = person.BelongCity;
                if (home != null)
                {
                    if (person.CurrentCity != home)
                        person.ChangeCurrentCity(home);
                }
                else if (person.IsWild || person.Invisible)
                {
                    // 本来就无势力归属：保持原地，由在下野/未发现名册里待着即可
                }
                else
                {
                    // 【修复】无户口武将下野时，落点优先用部队所在城（belongCity），
                    // 避免 CurrentCity 为 null（出征时在部队）导致 LeaveToWild 生成
                    // "在野却无城"的脏状态 —— 之后 BeRecruit 的 CurrentCity.RemoveWildPerson 会空引用崩溃。
                    PersonStateSystem.LeaveToWild(person, person.CurrentCity ?? belongCity, PersonChangeReason.Fixed);
                }
            });
            base.Clear();
            IsAlive = false;
            missionTarget = 0;
            missionTargetCell = null;
            missionType = 0;
            ActionOver = true;
            // Render 已在本方法开头统一销毁（见 isCleared 处的注释），这里不要再清一次。

            // 【新出兵】部队还没出城就被收尾（阵亡 / 入城解散）：
            // 解除城市的"出城进行中"登记，否则该城会一直以为有部队在出城而不再派兵。
            ClearNewEmittedFlag(belongCity);

            if (cell != null && cell.troop == this)
                cell.troop = null;

            for (int i = 0; i < StrategySkills.Count; i++)
                StrategySkills[i].Clear();

            if (landSkills != null)
                for (int i = 0; i < landSkills.Count; i++)
                    landSkills[i].Clear();

            if (waterSkills != null)
                for (int i = 0; i < waterSkills.Count; i++)
                    waterSkills[i].Clear();

            StrategySkills.Clear();
            landSkills?.Clear();
            waterSkills?.Clear();

            // 【泄漏修复】状态（Buff）也要清：Stun / Escape 等 BuffEffect 会订阅
            // OnTroopTurnStart 等全局事件，只在自身 Clear() 里退订。
            // 拆剧本时不清，它们会带着旧剧本的订阅活到下一次开局（实测每轮累积）。
            buffManager?.Clear();

            GameEvent.OnTroopClear?.Invoke(this, Scenario.Cur);

        }

        public void RemovePerson(Person person, bool justRemove = false)
        {
            if (person == null) return;

            // 摘除前先取所属城：武将摘光后主将为空，BelongCity 就推导不出来了，而收尾要用它摘城池名单
            City belongCity = BelongCity;

            if (Member1 == person)
            {
                Member1.mBelongTroop = null;
                Member1 = null;

                Member1 = Member2;
                Member2 = null;
            }
            else if (Member2 == person)
            {
                Member2.mBelongTroop = null;
                Member2 = null;
            }
            else if (Leader == person)
            {
                Leader.mBelongTroop = null;
                Leader = null;

                if (Member1 != null)
                {
                    Leader = Member1;
                    Member1 = Member2;
                    Member2 = null;
                }
            }

            // 武将已经全部离开：这支部队没有主体了，走 Clear 做完整收尾。
            // 必须放在 CalculateAttribute 之前 —— 后者按主将算属性，CalculateMaxTroops 会直接取 Leader，
            // 主将为空时会在那里空引用。
            // justRemove 的调用方（重新编组 / 换势力等）只是把武将摘出来，不销毁部队，故跳过。
            if (!justRemove && Leader == null && Member1 == null && Member2 == null)
            {
                ClearWithBelongCity(belongCity);
                return;
            }

            if (!justRemove)
            {
                // 重算属性前先按当前在队武将重建 Action:
                // RemovePerson 的部分调用方(下野/俘虏摘除/单挑摘除等)之后不会调 ResetActionAndStatus,
                // 若不在这里重建, 已离队武将装配的 Action(如辅佐的 TroopAssistAttack)仍订阅着
                // OnTroopCalculateAttribute, 会在下面的重算里把旧概率写回去, 部队会一直带着
                // 离队武将的能力直到下次 ResetActionAndStatus。
                // InitActionList 自带 isCleared 守卫, 收尾过的部队进来是无害空转。
                InitActionList();
                CalculateAttribute(Scenario.Cur);
            }
        }

        /// <summary>
        /// 【兜底】主将缺失时，由尚在的副将接任主将。
        ///
        /// 为什么需要：<see cref="BelongCity"/> / <see cref="BelongForce"/> 都是从主将实时推导的
        /// （<c>Leader?.BelongCity</c>）。主将一旦为空，任务取值器就不再给这支部队派任务
        /// （TroopMissionBehaviour 要求 BelongCity != null），DoAI 会直接判完成，
        /// 部队于是永久停在原地 —— 玩家观感就是"部队停在城池上不动"。
        ///
        /// <see cref="RemovePerson"/> 已经处理了"运行中主将被摘除"的情况，但下面这些路径仍会留下无主部队：
        ///   · 读存档：存档里写下的就是"主将已空、副将仍在"的中间状态；
        ///   · 任何绕过 RemovePerson 直接清空 Leader / LeaderId 的路径。
        /// 因此这里做一次统一修复，并在读档（OnScenarioPrepare）与 AI 行动前各兜一次。
        /// </summary>
        /// <returns>是否发生了接任</returns>
        public bool TryStepUpMemberToLeader()
        {
            if (Leader != null)
                return false;

            Person next = Member1 != null ? Member1 : Member2;
            if (next == null)
                return false;

            // 与 RemovePerson 的接任口径保持一致：副将1 顶上，副将2 前移
            if (Member1 != null)
            {
                Leader = Member1;
                Member1 = Member2;
                Member2 = null;
            }
            else
            {
                Leader = Member2;
                Member2 = null;
            }

            // 这几个位置可能来自旧数据，顺手把归属引用补齐
            // （只补空的，避免覆盖指向其它部队的有效归属）
            ForEachPerson(p =>
            {
                if (p != null && p.mBelongTroop == null)
                    p.mBelongTroop = this;
            });

            Sango.Log.Info($"[{Name}]主将缺失，由{Leader?.Name}接任主将");
            return true;
        }

        /// <summary>
        /// 加入某个势力,需要指定一个城市
        ///
        /// 【为什么要带一大段收尾】Troop 的 <see cref="BelongForce"/> / <see cref="BelongCorps"/> /
        /// <see cref="BelongCity"/> 都是主将的实时派生,主将一改归属就立刻跟着变,数据层看起来什么都不用做。
        /// 真正不会自动同步的有三样:
        ///   1. 所属城的 allTroops / allAttackTroops 名单 —— 只在组建、入城、收尾时维护;
        ///   2. 各势力记录的威胁名单 —— 换旗后旧仇家的 AI 仍会按名单派兵来"驱逐"它;
        ///   3. 表现层的势力色 —— 旗帜材质色与头顶血条底色只在模型加载时写入一次。
        /// 漏掉任一项都会出现"登庸成功、部队却还顶着敌国颜色、甚至还被当敌人打"的当回合不一致。
        /// </summary>
        /// <param name="city">主将即将归属的城市</param>
        /// <returns>是否发生了换旗(目标是同势力 / 参数为空时返回 false)</returns>
        public bool JoinToForce(City city)
        {
            if (city == null || Leader == null) return false;
            if (Leader.IsSameForce(city)) return false;

            City lastBelongCity = BelongCity;
            Force lastForce = BelongForce;

            // 先取快照再摘人:RemovePerson 会把副将2前移到副将1的位置,
            // 边遍历边删必然漏掉副将2,让他留在编制里还挂着敌方归属(等于部队里混着敌将)。
            Person[] members = new Person[] { Member1, Member2 };
            for (int i = 0; i < members.Length; i++)
            {
                Person mem = members[i];
                if (mem == null) continue;

                RemovePerson(mem, true);
                // 遣返回自己的所属城;所属城为空(城池已陷落)时退回所在城,避免把 null 塞进任务目标
                City homeCity = mem.BelongCity ?? mem.CurrentCity;
                if (homeCity != null)
                    mem.SetMission(MissionType.PersonReturn, homeCity);
                mem.ActionOver = true;
            }

            // 旧任务是旧东家下的命令(多半就是打新东家),留着会让刚归顺的部队继续敌对行为
            ClearMission();

            Leader.JoinToForce(city);
            ResetActionAndStatus();
            OnBelongForceChanged(lastForce, lastBelongCity);
            return true;
        }

        /// <summary>
        /// 所属势力变化后的统一收尾:迁移城市部队名单、清理各势力的威胁记录、刷新表现层势力色。
        /// </summary>
        /// <param name="lastForce">换旗前的所属势力,可为空</param>
        /// <param name="lastBelongCity">换旗前的所属城,可为空</param>
        void OnBelongForceChanged(Force lastForce, City lastBelongCity)
        {
            City newBelongCity = BelongCity;

            // 1. 城市部队名单迁移:名单只在组建 / 入城 / 收尾时维护,换旗必须手工搬,
            //    否则旧城的兵力与"来犯部队"统计里还留着它,新城部队面板里却没有它。
            if (lastBelongCity != newBelongCity)
            {
                lastBelongCity?.allTroops.Remove(this);
                if (IsListedAsAttackTroop)
                    lastBelongCity?.allAttackTroops.Remove(this);

                if (newBelongCity != null)
                {
                    if (!newBelongCity.allTroops.Contains(this))
                        newBelongCity.allTroops.Add(this);
                    if (IsListedAsAttackTroop && !newBelongCity.allAttackTroops.Contains(this))
                        newBelongCity.allAttackTroops.Add(this);
                }
            }

            // 2. 威胁名单:换旗后它对许多势力来说已经不是敌人(甚至成了友军),
            //    必须立刻摘掉,否则当回合内 AI 仍会出兵打自己人。
            Scenario scenario = Scenario.Cur;
            if (scenario != null)
            {
                // 【性能】原 ForEach(force => force?.RemoveThreatTroop(this)) 的 lambda 捕获 this，
                // 每次易主都要分配闭包 + 委托；改为按下标遍历底层数组。
                Force[] forceArray = scenario.forceSet.objects;
                for (int i = 1; i < forceArray.Length; i++)
                {
                    Force force = forceArray[i];
                    if (force != null)
                        force.RemoveThreatTroop(this);
                }
            }

            // 3. 表现层:旗帜 / 头顶血条底色在登庸后立刻按新势力色重刷
            Render?.UpdateRender();
            GameEvent.OnTroopChangeForce?.Invoke(this, lastForce, BelongForce);
        }

        /// <summary>
        /// 本部队是否属于"会登记进城市 allAttackTroops 的作战部队"。
        /// 口径与组建时的登记条件保持一致(战斗兵种 && 非 Id==1),搬运名单时两边必须用同一把尺子。
        ///
        /// public 是给 <see cref="City.RebuildTroopLists"/> 用的：重建名单时必须与
        /// 组建 / 换旗用同一把尺子，否则会出现"组建时进了、重建时漏掉"的分叉。
        /// </summary>
        public bool IsListedAsAttackTroop => LandTroopType != null && LandTroopType.isFight && LandTroopType.Id != 1;

        public void OnPersonChangeCity(Person person, City old_city, City new_city)
        {

        }


        /// <summary>
        /// 从 `from` 的相邻**己方**据点里，挑"从本部队当前位置出发代价最小"的一座。
        ///
        /// 【为什么用逆向 A* 而不是逐城比较】原版 `get_path_to_ring0/1/2` 就是
        /// 多目标一次搜索：把整圈候选放进 open，弹出起点时自然得到"哪座最优"。
        /// 逐城比较要跑 k 次搜索（k = 相邻据点数量），且每次都从零展开。
        ///
        /// 【代价口径】`Map.GetPathReverseAStar` 内部走 `PathAlignment.GetStepCost`，
        /// 已经包含地形消耗、敌我部队/建筑附加、ZOC、毒泉栈道、着火 —— 与原版 `UnitPath` 同口径。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="from">起点城（通常是部队当前所在区域的据点）</param>
        /// <param name="scenario">场景</param>
        /// <returns>最优的相邻己方据点；没有候选返回 null</returns>
        static City PickBestNeighborCity(Troop troop, City from, Scenario scenario)
        {
            if (troop == null || from == null || scenario == null || scenario.Map == null)
                return null;

            List<City> cities = new List<City>();
            List<Cell> targets = new List<Cell>();

            from.ForeachNeighborCities(c =>
            {
                if (c == null || !c.IsAlive) return;
                if (troop.BelongForce == null || c.BelongForce != troop.BelongForce) return;
                if (c.CenterCell == null) return;

                cities.Add(c);
                targets.Add(c.CenterCell);
            });

            if (cities.Count == 0)
                return null;
            if (cities.Count == 1)
                return cities[0];

            List<Cell> path = new List<Cell>();
            int cost = scenario.Map.GetPathReverseAStar(troop, targets, path);

            if (cost == Map.PathCostImpossible || path.Count == 0)
                return cities[0];   // 全不可达：退回原来的"就近"行为，不让部队卡死

            // 路径终点落在哪座城的中心格上，那座就是最优目标
            Cell last = path[path.Count - 1];
            for (int i = 0; i < cities.Count; i++)
            {
                if (cities[i].CenterCell == last)
                    return cities[i];
            }

            return cities[0];
        }

        public void SetMission(MissionType missionType, int missionTarget)
        {
            if(BelongForce == null) return;

            // 【空壳任务兜底】TroopProtectBuilding / TroopProtectTroop 目前没有行为实现
            // （DoAI 直接 return true、IsMissionComplete 抛 NotImplementedException），
            // 一旦被选中部队会原地不动、并在判定完成时抛异常。
            //   原版 Escort(6) 本身就是死代码（run_order 直接 result = false）；
            //   原版 Expel(5) 的语义已由 TroopProtectCity 承担（守城即驱逐来犯之敌）。
            // 所以这里统一改判为守城，确保任何入口（玩家菜单 / AI 改派 / 旧存档）都不会产生死任务。
            if (missionType == MissionType.TroopProtectBuilding
                || missionType == MissionType.TroopProtectTroop)
            {
                missionType = MissionType.TroopProtectCity;

                // ProtectBuilding 的目标是**建筑 Id**，而 ProtectCity 期望**城 Id**。
                // 直接改判会把建筑 Id 当城 Id 用（找不到城 → 任务空转）。
                // 这里先按城 Id 校验一次，不匹配就退化为"守本城"。
                Scenario currentScenario = Scenario.Cur;
                if (currentScenario != null && FindCityById(currentScenario, missionTarget) == null)
                    missionTarget = BelongCity != null ? BelongCity.Id : -1;
            }

            // 【委任部队】玩家第一军团不使用面向势力 AI 的任务枚举：
            // 这里统一把 TroopXXX 映射为 PlayerTroopXXX（见 TroopMissionBehaviour.ToPlayerMission）。
            // 映射收敛在这一处，于是所有派发点——UI 指令层（TroopInteractive*.cs /
            // CityTransport / TroopActionBuild）与行为类内部的改派代码——都不需要关心
            // "这支部队是不是委任部队"。
            if (IsPlayerControl)
                missionType = TroopMissionBehaviour.ToPlayerMission(missionType);

            // 【日志格式】原写法 `任务变更:XxxCity -> 13` 里的 13 是**目标 Id**，
            // 极易被误读成"任务类型变成了 13（TroopTransformGoodsToCity）"。
            // 改为显式标注目标，避免排查时看错。
            Sango.Log.Info($"{BelongForce.Name}的[{Name}] 部队任务 → {missionType}（目标 Id={missionTarget}）");
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget;
            NeedPrepareMission();
        }



        public void ClearMission()
        {
            this.missionType = 0;
            this.missionTarget = 0;
            this.missionTargetCell = null;
            this.missionParams1 = 0;
            this.missionParams2 = 0;
        }

        /// <summary>
        /// 【委任部队】把任务枚举归一化为委任专用版本（PlayerTroopXXX）。
        ///
        /// 【为什么需要】在物理分离之前，玩家第一军团的部队与势力 AI 共用同一套
        /// TroopXXX 枚举，靠行为类内部的 `if (troop.IsPlayerControl)` 补丁区分收尾行为。
        /// 补丁已随物理分离删除（语义改由 PlayerTroopXxx 行为类承担），
        /// 于是旧存档里玩家部队保存的 TroopXXX 会落到势力 AI 版行为类上，
        /// 任务完成后会"自动改派返城"而不是交回玩家。读档后转换一次即可对齐。
        ///
        /// 【幂等性】以下情况不做任何事，因此可以安全地多次调用：
        ///   · 不属于玩家第一军团（AI 势力 / 玩家非主军团部队，本就该走势力 AI）
        ///   · 尚无任务（missionType &lt;= 0）
        ///   · 已是 PlayerTroopXXX
        ///   · 该方法对 AI 专属任务（补给 / 求援 / 协防城池等）原样返回，不产生变化
        /// </summary>
        public void NormalizeAppointMission()
        {
            if (!IsPlayerControl)
                return;
            if (missionType <= 0)
                return;

            MissionType current = (MissionType)missionType;
            if (TroopMissionBehaviour.IsPlayerMission(current))
                return;

            MissionType mapped = TroopMissionBehaviour.ToPlayerMission(current);
            if (mapped == current)
                return;

            missionType = (int)mapped;
            NeedPrepareMission();
        }

        TroopMissionBehaviour troopMissionBehaviour;
        public TroopMissionBehaviour TroopMissionBehaviour
        {
            get
            {
                if (missionType == 0 && BelongCity != null && !IsPlayerControl)
                {
                    SetMission(MissionType.TroopReturnCity, BelongCity.Id);
                    NeedPrepareMission();
                }

                if (missionType == 0)
                {
                    return null;
                }

                if (this.troopMissionBehaviour == null || (int)troopMissionBehaviour.MissionType != missionType)
                {
                    troopMissionBehaviour = TroopMissionBehaviour.Create(missionType);

                    // 【为什么不在这里 Prepare】
                    // 行为对象的 Prepare 是一次完整的目标扫描（Profiler 里
                    // TroopOccupoyCity.Prepare 单帧就有 11 ms / 283 KB GC），
                    // 把它塞进属性 getter 意味着"任何一次读取属性都可能触发一次全量重算"，
                    // 调用方完全无从感知 —— 这是典型的隐藏副作用。
                    //
                    // 现在 getter 只负责「按当前 missionType 拿到或创建行为对象」，
                    // 真正准备由 DoAI 流程里显式的 `if (!isMissionPrepared)` 分支完成（见 DoAI）。
                    // 这里用 NeedPrepareMission() 把 isMissionPrepared 置 false，
                    // 保证新建出来的行为对象一定会被那一段补上 Prepare —— 不会漏准备。
                    NeedPrepareMission();
                }
                return troopMissionBehaviour;
            }
        }

        public void NeedPrepareMission()
        {
            isMissionPrepared = false;
        }

        public override bool DoAI(Scenario scenario)
        {
            // 【兜底防护】兵力已经归零、但还存活的"幽灵部队"。
            // 成因：SupplyTroop 一类直接改 troops 的路径没有走 ChangeTroops，
            // 不会把 IsAlive 置为 false，于是它仍能通过 CorpsAI.AITroops 的存活过滤继续行动；
            // 执行返城等任务抵达据点后，会在 EnterCity 处以零为分母崩溃。
            // 这里统一收尾，保证 IsAlive 与 troops 始终一致，避免同类崩溃从其它路径再次出现。
            if (troops <= 0)
            {
                IsAlive = false;
                Clear();
                return true;
            }

            // 【兜底】主将缺失（阵亡 / 被俘 / 旧存档中间态）时先让副将接任：
            // 否则 BelongCity 推导为 null，下面的任务取值器会直接返回 null，
            // 这支部队就会永久停在原地（观感：停在城池上不动）。
            if (Leader == null)
            {
                if (TryStepUpMemberToLeader())
                {
                    // 接任者带兵上限可能不同，重算一次（该计算会用到归属城，故加保护）
                    if (Leader != null && Leader.BelongCity != null)
                        CalculateMaxTroops(scenario);
                }
                else if (liveDays <= 1)
                {
                    // 无将可接任：说明这支部队已经没有主体了。
                    // 这里只告警一次（liveDays 每回合自增，因此不会刷屏），不做静默清理——
                    // 兵力 / 兵装属于玩家或势力的资产，需要人工或后续规则处理。
                    Sango.Log.Warning($"[{Name}]部队既没有主将也没有副将，无法行动（坐标 {cell?.x},{cell?.y}）");
                }
            }

            if (AIFinished)
                return true;

            // 【出城兜底·续】上一帧任务已结束、出城移动动画尚未播完：
            // 继续完成出城，不再重跑任务行为（防止任务被重复结算）。
            if (leavingBuildingCell)
            {
                if (!TroopAIUtility.TryLeaveBuildingCell(this, scenario))
                    return false;                   // 出城移动动画仍在播，下一帧继续

                GameEvent.OnTroopAIEnd?.Invoke(this, scenario);
                AIFinished = true;
                troopMissionBehaviour = null;
                leavingBuildingCell = false;
                ActionOver = true;
                return true;
            }

            if (!AIPrepared)
            {
                // 【委任部队】兜底归一化：正常情况下 Init 已处理过一次，
                // 这里再调一次是为了覆盖"运行中部队归属发生变化"的边界
                //（例如玩家部队被编入 / 移出第一军团）。
                // 方法本身幂等且开销极小（几次 int 比较）。
                NormalizeAppointMission();

                // 【AI 分流】按部队的 AI 类型选择"行动前准备"策略（见 TroopAIType）：
                //   · Faction → 完整启发式 AI
                //   · Appoint → 委任 AI，不做任何判断，单纯执行被指派的任务
                //   · None    → 无任务，不准备
                PrepareByAIType(scenario);
                AIPrepared = true;
                GameEvent.OnTroopAIStart?.Invoke(this, scenario);
            }

            TroopMissionBehaviour temp = TroopMissionBehaviour;
            if (temp == null)
            {
                // 【出城兜底】无任务部队滞留在城池 / 关隘 / 港口等建筑格上时执行出城，
                // 避免永久滞留在城头（部队不允许停留在建筑格上）。
                // 仅对非玩家控制的部队生效：受玩家控制的部队（含委任 / 第一军团）停在城头
                // 时由玩家自行决策，AI 不替玩家挪动。
                if (!IsPlayerControl && TroopAIUtility.IsOnBuildingCell(this))
                {
                    if (!TroopAIUtility.TryLeaveBuildingCell(this, scenario))
                        return false;               // 出城移动动画未播完，下一帧继续
                }

                GameEvent.OnTroopAIEnd?.Invoke(this, scenario);
                AIFinished = true;
                ActionOver = true;
                return true;
            }

            if (!isMissionPrepared)
            {
                temp.Prepare(this, scenario);
                isMissionPrepared = true;

                // 【修复】Prepare 内部可能通过 SetMission 切换任务（例如 TroopOccupyCity.Prepare 在
                // 目标失效时改为返城、TroopProtectCity.Prepare 在无敌情时改为返城）。此时 temp 仍指向
                // 旧任务的行为对象，继续执行会造成"旧对象跑新任务"——本回合空转，且 TargetCity /
                // priorityActionData 与新任务不匹配。
                // 切换可能链式发生（进攻 → 协防 → 返城），因此循环刷新直到任务与行为对象一致。
                bool needFallbackRetreat = false;
                for (int guard = 0; guard < 4; guard++)
                {
                    TroopMissionBehaviour refreshed = TroopMissionBehaviour;
                    if (refreshed == null)
                    {
                        needFallbackRetreat = true;
                        break;
                    }

                    // 任务与行为对象已一致，无需继续刷新
                    if (refreshed == temp)
                        break;

                    temp = refreshed;

                    // 新任务的行为对象可能尚未准备（例如被 NeedPrepareMission 标记过），补一次准备
                    if (!isMissionPrepared)
                    {
                        temp.Prepare(this, scenario);
                        isMissionPrepared = true;
                    }
                }

                // 【原版 unit_run_order】命令执行不出来时，再给一次"撤退 + 重试"的机会。
                // 出处：s11_game_ai_unit.cpp:429-448
                //   run_order 失败 && 未行动完成 && 有命令 && 非玩家控制 → unit_retreat → 再 run_order
                //
                // 【语义注意】本项目 DoAI 的返回值是"动作未完成"（移动动画等），不能直接当"命令失败"用；
                // 这里只在"任务行为对象根本建不出来"（即命令无法执行）时才触发，语义与原版一致。
                if (needFallbackRetreat)
                {
                    bool canFallback = AIConfig.Instance.useStrictOpen311UnitAI
                        && !ActionOver
                        && missionType != (int)MissionType.None
                        && !IsPlayerControl;

                    if (canFallback)
                    {
                        RetreatAIOrder();
                        temp = TroopMissionBehaviour;
                        if (temp != null)
                        {
                            temp.Prepare(this, scenario);
                            isMissionPrepared = true;
                        }
                    }

                    if (temp == null)
                    {
                        GameEvent.OnTroopAIEnd?.Invoke(this, scenario);
                        AIFinished = true;
                        ActionOver = true;
                        return true;
                    }
                }
            }

#if SANGO_DEBUG_AI
            if (GameAIDebug.Instance.WaitForShowAIPrepare())
                return false;
#endif
            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append("1,");
            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append($"12:{temp.GetType()},");
            if (!temp.DoAI(this, scenario))
                return false;

            // 【出城兜底】纯保底：仅在任务本回合动作已完成、部队却仍站在建筑格上时，
            // 才就近移出城头。正常行军的任务（攻击 / 占领 / 移动 / 返城等）在 DoAI 内
            // 已离开城头，这里不会触发；只有"任务结束却仍赖在城头"的异常部队
            // （如 TroopStay 待命、任务改写后停摆）才被移出，不再拦截部队首回合的正常行军。
            // 仅对非玩家控制的部队生效：受玩家控制的部队（含委任 / 第一军团）由玩家自行决策。
            if (IsAlive && !IsPlayerControl && TroopAIUtility.IsOnBuildingCell(this))
            {
                // 出城移动动画未播完：标记续跑，下一帧继续出城而不重跑任务
                if (!TroopAIUtility.TryLeaveBuildingCell(this, scenario))
                {
                    leavingBuildingCell = true;
                    return false;
                }
            }

            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append("13,");
            GameEvent.OnTroopAIEnd?.Invoke(this, scenario);
            AIFinished = true;
            troopMissionBehaviour = null;
            leavingBuildingCell = false;

            // 【新出兵兜底】本回合行动已经结束却仍没走出城头（例如被敌军彻底围死），
            // 必须解除城市的"出城进行中"登记，否则该城会永久无法再派兵。
            if (isNewEmitted)
                ClearNewEmittedFlag(null);

            ActionOver = true;
            return true;
        }

        /// <summary>
        /// 【AI 分流】按部队的 <see cref="AIType"/> 执行对应的"行动前准备"。
        ///
        /// 两类 AI 共用同一个任务执行层（<see cref="TroopMissionBehaviour"/>），
        /// 差别只在准备阶段：
        ///   · <see cref="TroopAIType.Faction"/> → <see cref="AIPrepare"/>（完整启发式）
        ///   · <see cref="TroopAIType.Appoint"/> → <see cref="AppointPrepare"/>（纯净，无任何判断）
        ///   · <see cref="TroopAIType.None"/>    → 不做任何准备
        /// </summary>
        /// <param name="scenario">场景对象</param>
        void PrepareByAIType(Scenario scenario)
        {
            switch (AIType)
            {
                case TroopAIType.Faction:
                    AIPrepare(scenario);
                    break;

                case TroopAIType.Appoint:
                    AppointPrepare(scenario);
                    break;
            }
        }

        /// <summary>
        /// 【委任 AI】玩家第一军团委任部队的"行动前准备"。
        ///
        /// 该方法**刻意保持为空**：委任的语义就是"照玩家指派的任务单纯执行"，
        /// 因此这里不做任何战场态势判断，也不改派 / 追加任务。
        ///
        /// 以下都是势力 AI 才有的额外逻辑，委任部队一律不参与：
        ///   · 态势分档与主动撤退（<see cref="TroopBattleTier"/>）
        ///   · 断粮 / 兵少 / 低士气的就近补给撤退
        ///   · 向补给队求援（<see cref="MissionType.TroopAskSupply"/>）
        ///   · 随军增筑辅助建筑（<see cref="TryBuildFieldBuilding"/>）
        ///   · 部队角色自动推导（<see cref="ResolveRole"/>）
        ///
        /// 保留此方法（而不是干脆不调用）是为了给"委任 AI"一个明确的落点：
        /// 将来若确有委任专属的准备工作，应当加在这里，而不是往势力 AI 分支里塞条件判断。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        void AppointPrepare(Scenario scenario)
        {
            // 委任 AI：无任何额外逻辑，直接进入任务执行层。
        }

        #region 原版部队 AI 直译（GameAI::unit_update_order / unit_retreat / unit_has_invalid_target）

        /// <summary>
        /// 【原版直译】部队命令更新 —— GameAI::unit_update_order（s11_game_ai_unit.cpp:99-315）。
        ///
        /// 原版语义：每回合行动前，按"出征意图（deploy_type）+ 当前状态"决定：
        ///   · 继续执行原任务；
        ///   · 改派成别的任务；
        ///   · 或转入撤退（unit_retreat）。
        /// 方法最后必然落到一个结论（原版是"要么 return true，要么掉到末尾就撤退"）。
        ///
        /// 【本项目差异说明】原版有几处依赖 Sango 没有的数据结构，这里做了标注：
        ///   1. `request_force`（国境求援）—— Sango 无部队级字段，整段跳过；
        ///   2. `kyoten state == Battle / enemy_unit_count`（据点战斗态）——
        ///      Sango 无据点级战斗态缓存，这段"被围时转防卫"的逻辑暂不实现；
        ///   3. `district policy`（军团政策目标）—— Sango 用 Corps.appoint 表达，
        ///      语义不同，放到 HasInvalidMissionTarget 里按"目标是否己方/是否敌对"近似。
        /// 其余分支逐行照搬。
        /// </summary>
        /// <returns>true = 本回合处理完毕；false = 需要中断（原版 stop_）</returns>
        public bool UpdateAIOrder(Scenario scenario)
        {
            if (scenario == null || !IsAlive)
                return true;

            // :103-104 非正规势力（贼军 / 异民族）不参与命令更新
            Force force = BelongForce;
            if (force == null || !force.IsNormal)
                return true;

            // :107-113 玩家部队：直属军团由玩家自己指挥，其余做"目标失效"体检
            if (IsPlayerControl)
            {
                bool isPlayerMainCorps = BelongCorps != null && BelongCorps.number == 1;
                if (!isPlayerMainCorps && HasInvalidMissionTarget(scenario))
                    return RetreatAIOrder();
            }

            // :114-123 求援部队（request_force >= 0）
            //
            // 原版语义：响应援军请求而来的部队**不参与正常的意图改派**，
            // 只做一件事 —— 判断是否可以撤回。任一条件成立就撤退：
            //   · 断粮 / 兵力 ≤ 500
            //   · 与求援方变成敌对（同盟破裂）
            //   · 目标对象已消失
            //   · 目标不再敌对
            // 全部不成立则保持当前任务继续支援。
            int requestForce = requestForceId;
            if (requestForce >= 0)
            {
                Force requestForceObj = scenario.forceSet != null ? scenario.forceSet.Get(requestForce) : null;
                City requestCity = GetMissionTargetCity(scenario);

                bool requestInvalid =
                    food < PrepeareFoodCost()
                    || troops <= 500
                    || requestForceObj == null
                    || IsEnemy(BelongForce, requestForceObj)                       // 与求援方反目
                    || requestCity == null
                    || !requestCity.IsAlive
                    || (requestCity.BelongForce != null && !IsEnemy(BelongForce, requestCity.BelongForce));

                if (requestInvalid)
                {
                    requestForceId = -1;
                    return RetreatAIOrder();
                }

                return true;
            }

            // :124-130 读意图层
            TroopDeployType dType = (TroopDeployType)deployType;
            City deployCity = GetDeployTargetCity(scenario);

            // :132-163 撤退判定
            //   原版：food_shortage 或 (非猪突 且 兵力 <= 500)
            //   再叠加：攻城兵器若所有可用战法的气力都不够，同样撤退
            // 【性格来源】"是否猪突"读性格上的 isChototsu 标记，不依赖性格枚举取值，
            // 新增性格只需在 Personalities.json 里勾上该字段。
            bool chototsu = LeaderPersonality != null && LeaderPersonality.isChototsu;
            bool foodShortage = food < PrepeareFoodCost();
            bool needRetreat = foodShortage || (!chototsu && troops <= 500);

            if (!needRetreat && dType != TroopDeployType.None && HasSiegeEquipment() && !HasUsableTactic())
                needRetreat = true;

            switch (dType)
            {
                case TroopDeployType.Attack:
                    if (!needRetreat)
                    {
                        // :185-198 出发据点正在被围攻 → 就地转防卫回援。
                        // 原版条件（全部满足）：
                        //   ① 所属据点存在且是自己的；
                        //   ② 据点处于战斗状态（kyoten state == Battle）；
                        //   ③ 部队当前站在自己的据点领地上（hex.area_building_id == service_id）；
                        //   ④ 围攻敌军数 >= 城内空闲武将数（守不住）；
                        //   ⑤ 自己不是冲车（EquipmentId_Shousha 不回援——它拆城比守城有用）。
                        // 【近似】第 ② 条本项目没有据点战斗态缓存，用"据点影响范围内有敌军"代替。
                        if (IsServiceUnderSiege(scenario, out City besieged))
                        {
                            deployType = (int)TroopDeployType.Defense;
                            deployTarget = besieged.Id;
                            if ((MissionType)missionType != MissionType.TroopProtectCity)
                                SetMission(MissionType.TroopProtectCity, besieged.Id);
                            return true;
                        }

                        if (IsValidDeploy(dType, deployCity))
                        {
                            // :200-214 目标仍可攻：仅在"无令/待机/撤退中"时改派攻击
                            switch ((MissionType)missionType)
                            {
                                case MissionType.TroopMovetoCell:
                                    if (missionTargetCell != null) break;   // 原版 is_valid_pos(order_target_pos)
                                    goto case MissionType.TroopStay;

                                case MissionType.TroopMovetoCity:
                                    if (missionTarget >= 0) break;          // 移动令的目标城有效
                                    goto case MissionType.TroopStay;

                                case MissionType.TroopStay:
                                    // :210 原版 rand_bool(50) ? Attack : Subjugate
                                    if (deployCity != null)
                                        SetMission(GameRandom.Chance(50) ? MissionType.TroopDestroyTroop : MissionType.TroopOccupyCity, deployCity.Id);
                                    break;
                            }
                            return true;
                        }

                        // :215-225 目标已经变成己方（城被打下了 / 部队归顺）：转为移动过去
                        if (deployCity != null && deployCity.BelongForce == force)
                        {
                            switch ((MissionType)missionType)
                            {
                                case MissionType.TroopMovetoCell:
                                case MissionType.TroopMovetoCity:
                                case MissionType.TroopReturnCity:
                                    return true;
                            }
                            SetMission(MissionType.TroopMovetoCity, deployCity.Id);
                            return true;
                        }
                    }
                    break;

                case TroopDeployType.Defense:
                    if (!needRetreat)
                    {
                        // :231-248 目标据点失守时，原版允许"就近改判到相邻的己方据点"：
                        //   ① building = 部队当前所在区域的据点，必须存在且是自己的；
                        //   ② ai_table_get_kyoten_distance(deploy_target, building, force) == 1（两据点相邻）；
                        //   ③ building 本身也要满足 Defense 的合法性。
                        // 本项目用 City.NeighborList（相邻城市表）承担第 ② 条。
                        if (!IsValidDeploy(dType, deployCity))
                        {
                            City here = cell != null ? cell.BelongCity : null;
                            if (here == null || !here.IsAlive || here.BelongForce != force)
                                break;
                            if (deployCity == null || !deployCity.IsNeighborKyoten(here))
                                break;

                            // 原版只取"部队当前所在区域的据点"（`here`）；
                            // 这里用一次多目标逆向 A*，在**所有相邻己方据点**里挑代价最小的，
                            // 避免"取第一个相邻城"导致部队绕远路。
                            City best = PickBestNeighborCity(this, here, scenario);
                            if (best == null) best = here;

                            if (!IsValidDeploy(dType, best))
                                break;
                            deployCity = best;
                        }

                        // :249-261 原版改为 Expel（驱逐），本项目对应"守城"
                        switch ((MissionType)missionType)
                        {
                            case MissionType.TroopMovetoCell:
                                if (missionTargetCell != null) break;
                                goto case MissionType.TroopStay;

                            case MissionType.TroopMovetoCity:
                                if (missionTarget >= 0) break;
                                goto case MissionType.TroopStay;

                            case MissionType.TroopStay:
                                if (deployCity != null)
                                    SetMission(MissionType.TroopProtectCity, deployCity.Id);
                                break;
                        }
                        return true;
                    }
                    break;

                case TroopDeployType.Transport:
                    if (!IsValidDeploy(dType, deployCity))
                        break;

                    // :267-276 输送队：唯一命令就是"移动到目标据点"
                    //   原版 UnitOrderId_Move 到达己方据点后由 unit_enter 自动卸货，
                    //   本项目与之对应的任务不是 TroopMovetoCity（那只是"走过去"，到达后不做物资结算），
                    //   而是 TroopTransformGoodsToCity（移动到目标城 + 抵达后 EnterCity 卸货）。
                    //   判据同样照抄原版：已在执行且目标准确 → 保持不动（:269-271 的 break）。
                    MissionType transportMission = (MissionType)missionType;

                    // :269-271 已经在移动且目标坐标有效 → 保持不动
                    if (transportMission == MissionType.TroopMovetoCell && missionTargetCell != null)
                        return true;

                    // 补给队 / 求援：missionTarget 是**友军部队 Id**，与 deployTarget（城 Id）不同域，
                    // 不能参与城的比对；只要任务本身还在运输类就保持，避免被改判成"运物资到城"。
                    if (transportMission == MissionType.TroopSupplyTroop
                        || transportMission == MissionType.TroopAskSupply)
                        return true;

                    // 运物资到城：目标城必须与 deployTarget 一致才保持。
                    if (transportMission == MissionType.TroopTransformGoodsToCity
                        && deployCity != null
                        && missionTarget == deployCity.Id)
                        return true;

                    if (deployCity != null)
                        SetMission(MissionType.TroopTransformGoodsToCity, deployCity.Id);
                    return true;

                case TroopDeployType.Build:
                    if (needRetreat)
                        break;
                    // :281 原版要求携带金 >= 200，否则放弃工程
                    if (gold < 200)
                        break;
                    if (!IsValidDeploy(dType, deployCity))
                        break;

                    // :285-291 工程队无令/待机 → 直接撤退
                    switch ((MissionType)missionType)
                    {
                        case MissionType.TroopStay:
                            return RetreatAIOrder();
                    }
                    return true;

                case TroopDeployType.None:
                    if (missionType == (int)MissionType.None || missionType == 0)
                    {
                        // :293-301 无意图且无令：脚下若是有主/敌方据点就地征服，否则撤退
                        City here = cell != null ? cell.BelongCity : null;
                        if (here == null || here.BelongForce != force)
                            return RetreatAIOrder();
                        SetMission(MissionType.TroopOccupyCity, here.Id);
                        return true;
                    }
                    goto default;

                default:
                    // :304-310 其它情况：无令 / 待机 → 撤退
                    switch ((MissionType)missionType)
                    {
                        case MissionType.None:
                        case MissionType.TroopStay:
                            return RetreatAIOrder();
                    }
                    return true;
            }

            // :312-314 到了这里说明上面的分支都没结论
            if ((MissionType)missionType == MissionType.TroopReturnCity)
                return true;
            return RetreatAIOrder();
        }

        /// <summary>
        /// 【原版直译】撤退 —— GameAI::unit_retreat（s11_game_ai_unit.cpp:378-399）。
        ///
        /// 原版：把命令设成 Retreat，目标坐标 = 所属据点（service）的坐标。
        /// 所属据点不存在时退化为 Subjugate（本项目无对应命令，直接放弃任务）。
        /// </summary>
        bool RetreatAIOrder()
        {
            if (!IsAlive)
                return false;

            City service = BelongCity;
            if (service == null)
            {
                // 原版：unit_set_order(unit, UnitOrderId_Subjugate, NullPos, -1)
                // 本项目没有"空目标征服"，用返城到当前所在地兜底
                SetMission(MissionType.TroopMovetoCity, -1);
                return true;
            }

            SetMission(MissionType.TroopMovetoCity, service.Id);
            return true;
        }

        /// <summary>
        /// 【原版直译】目标失效检测 —— GameAI::unit_has_invalid_target（s11_game_ai_unit.cpp:317-376）。
        ///
        /// 原版判定（部队型）：
        ///   1. 当前命令是 Expel（驱逐）时豁免；
        ///   2. 目标对象已消失；
        ///   3. 目标是自己势力；
        ///   4. 目标势力与自己不再是敌对；
        ///   5. 军团政策目标（CaptureForce / CaptureCity）与当前目标不符。
        ///
        /// 【差异】第 5 条依赖原版 DistrictPolicy，本项目的 Corps.appoint 语义不同，
        /// 暂只实现 2/3/4 三条；第 1 条的 Expel 对应本项目 TroopProtectCity。
        /// </summary>
        public bool HasInvalidMissionTarget(Scenario scenario)
        {
            if (scenario == null || !IsAlive)
                return false;

            // :327-328 驱逐（守城）命令豁免
            if ((MissionType)missionType == MissionType.TroopProtectCity)
                return false;

            if (IsTransport)
            {
                // :367-373 输送队：目标必须存在且是自己势力
                City target = GetMissionTargetCity(scenario);
                if (target == null || !target.IsAlive)
                    return true;
                if (target.BelongForce != BelongForce)
                    return true;
                return false;
            }

            // :331-342 作战部队
            City missionCity = GetMissionTargetCity(scenario);
            if (missionCity == null || !missionCity.IsAlive)
                return true;
            if (missionCity.BelongForce == BelongForce)
                return false;                                    // 目标变成自己的，交给 UpdateAIOrder 处理
            if (missionCity.BelongForce != null && !IsEnemy(BelongForce, missionCity.BelongForce))
                return true;                                     // 外交变化：不再敌对

            return false;
        }

        /// <summary>取意图目标对应的据点（原版 get_building(deploy_target)）</summary>
        City GetDeployTargetCity(Scenario scenario)
        {
            return FindCityById(scenario, deployTarget);
        }

        /// <summary>取当前任务目标对应的据点</summary>
        City GetMissionTargetCity(Scenario scenario)
        {
            return FindCityById(scenario, missionTarget);
        }

        /// <summary>按 Id 在 citySet 里找据点（城 / 关 / 港统一建模）</summary>
        static City FindCityById(Scenario scenario, int id)
        {
            if (scenario == null || scenario.citySet == null || id < 0)
                return null;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City c = scenario.citySet[i];
                if (c != null && c.Id == id)
                    return c;
            }
            return null;
        }

        /// <summary>
        /// 【原版直译】意图合法性 —— GameAI::is_valid_deploy（s11_game_ai_unit.cpp:401-427）。
        ///   Attack：目标存在、不是自己势力、且与自己敌对（或无主）；
        ///   Defense / Transport / Build：目标必须是自己势力。
        /// </summary>
        bool IsValidDeploy(TroopDeployType type, City target)
        {
            if (target == null || !target.IsAlive)
                return false;

            Force self = BelongForce;
            switch (type)
            {
                case TroopDeployType.Attack:
                    if (target.BelongForce == self)
                        return false;
                    if (target.BelongForce != null && !IsEnemy(self, target.BelongForce))
                        return false;
                    break;

                case TroopDeployType.Defense:
                case TroopDeployType.Transport:
                case TroopDeployType.Build:
                    if (target.BelongForce != self)
                        return false;
                    break;
            }
            return true;
        }

        /// <summary>本部队是否为攻城器械（按配置的 siegeRole 判定，不认兵种 Id）</summary>
        bool HasSiegeEquipment()
        {
            TroopType tt = TroopType;
            return tt != null && tt.siegeRole != 0;
        }

        /// <summary>
        /// 原版 s11_game_ai_unit.cpp:145-161：遍历兵装可用的战法，只要有一个气力够就算"还能打"。
        /// </summary>
        bool HasUsableTactic()
        {
            List<SkillInstance> list = skills;
            if (list == null || list.Count == 0)
                return false;
            for (int i = 0; i < list.Count; i++)
            {
                SkillInstance skill = list[i];
                if (skill == null) continue;
                if (energy >= skill.costEnergy)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 【原版直译】所属据点是否正被围攻且守不住 —— unit_update_order 的 Attack 分支前置判定
        /// （D:\open311\s11\s11_game_ai_unit.cpp:185-198）。
        ///
        /// 原版五个条件：
        ///   ① 所属据点存在、且是自己的；
        ///   ② 据点处于战斗状态（<c>kyoten[service].state == KyotenState_Battle</c>）；
        ///   ③ 部队当前站在自己据点的领地上（<c>hex.get_area_building_id(false) == service_id</c>）；
        ///   ④ 围攻敌军数 &gt;= 城内空闲武将数（<c>enemy_unit_count &gt;= idle_person_count</c>）；
        ///   ⑤ 自己不是冲车（<c>EquipmentId_Shousha</c> 不回援）。
        ///
        /// 【说明】第 ② 条用 <see cref="City.IsBattleState"/>（领地内敌军数 &gt; 0），
        /// 与原版 KyotenState_Battle 的定义完全一致；第 ④ 条取 <see cref="City.FreePersonCount"/>。
        /// </summary>
        bool IsServiceUnderSiege(Scenario scenario, out City service)
        {
            service = BelongCity;
            if (service == null || !service.IsAlive)
                return false;

            // ③ 必须在自家据点领地上
            City area = cell != null ? cell.BelongCity : null;
            if (area != service)
                return false;

            // ⑤ 冲车不回援：它拆城比守城有用
            TroopType tt = TroopType;
            if (tt != null && tt.siegeRole == (int)SiegeRoleType.Ram)
                return false;

            // ② 据点处于战斗状态 —— 原版定义就是"据点区域内存在敌军"（s11_enum_scen.h:63），
            //    统计口径见 City.CountEnemyUnitsInTerritory（领地 = OccupyCellList）。
            int enemyCount = service.CountEnemyUnitsInTerritory();
            if (enemyCount <= 0)
                return false;

            // ④ 敌军数压过城内可应战的武将数 → 守不住，需要在外部队回援
            //    原版 idle_person_count = "명령 가능한 무장 수"（可下令的武将数）
            return enemyCount >= service.FreePersonCount;
        }

        #endregion

        /// <summary>
        /// 【势力 AI】AI 势力（及玩家势力非主军团）部队的行动前准备：完整的启发式流程
        /// （态势撤退 / 就近补给 / 求援 / 随军增筑 / 角色推导等）。
        ///
        /// 注意：本方法只应由 <see cref="PrepareByAIType"/> 在
        /// <see cref="TroopAIType.Faction"/> 分支下调用。方法开头的
        /// <c>IsPlayerControl</c> 判断是防御性的双保险，防止被其它路径直接调用时
        /// 让委任部队误入势力 AI 流程。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public void AIPrepare(Scenario scenario)
        {
            if (IsPlayerControl)
                return;

            // 【严格对齐原版】走 unit_update_order 的直译（<see cref="UpdateAIOrder"/>）。
            // 决策只看"出征意图 + 断粮 / 兵少 / 器械气力"，与下面这套本项目启发式互斥，
            // 由 AIConfig.useStrictOpen311UnitAI 切换（默认严格）。
            //
            // 注意：role 的固化（ResolveRole）在严格模式下仍然保留——它是**评分层**的输入，
            // 不参与"打不打 / 撤不撤"的分支（原版也没有这个概念）；
            // 运输队同样要走原版分支（DeployType_Transport），所以放在 IsTransport 之前。
            if (AIConfig.Instance.useStrictOpen311UnitAI)
            {
                if (role == TroopRole.Auto)
                    role = ResolveRole();

                UpdateAIOrder(scenario);
                return;
            }

            if (IsTransport)
                return;

            AIConfig aiConfig = AIConfig.Instance;

            // 【分级策略】首次行动时固化部队角色（Auto → 按当前任务与自身属性推导一次）。
            // 角色决定评分权重与作战风格，固化后不再随回合变化，避免行为抖动。
            if (role == TroopRole.Auto)
                role = ResolveRole();

            // 【需求4】状态不佳且附近有可补给的补给队 → 切换为"求援"
            // 【前线建筑】工兵（TroopBuildBuilding / TroopFixBuilding）不参与求援：
            //   一是施工任务优先级更高，二是施工行为类用 missionParams1 记录"连建次数"，
            //   若被求援逻辑改写会导致连建计数错乱。
            bool isEngineerMission = missionType == (int)MissionType.TroopBuildBuilding
                                  || missionType == (int)MissionType.TroopFixBuilding;
            if (!isEngineerMission
                && missionType != (int)MissionType.TroopAskSupply
                && missionType != (int)MissionType.TroopReturnCity
                && missionType != (int)MissionType.TroopMovetoCity
                && IsNeedAskSupply(scenario, out Troop supplier))
            {
                // 【修复】先把原任务记录到 missionParams,求援结束后恢复,
                // 避免"求援→补满→任务被清空→自动返城"导致原进攻 / 防守任务永久丢失。
                missionParams1 = (int)missionType;
                missionParams2 = missionTarget;

                SetMission(MissionType.TroopAskSupply, supplier.Id);
                NeedPrepareMission();
                Sango.Log.Info($"{BelongForce?.Name}的[{Name}]状态不佳,向补给队[{supplier.Name}]求援!");
                return;
            }

            // 已处于返城 / 进城任务,无需重复撤退
            if (missionType == (int)MissionType.TroopReturnCity || missionType == (int)MissionType.TroopMovetoCity)
                return;

            // 【出征回合门槛】刚出城的部队不允许立刻掉头回城 / 撤离。
            //
            // 成因：态势评级看的是"局部战力比"，刚出城的部队经常一出门就被评成劣势 / 危局；
            // 而"最近的己方据点"往往就是它刚出的那座城，于是判定撤退后立刻折返，
            // 在城池格上反复进出，玩家观感就是"部队停在城池上不动"。
            // 现在要求部队在野外至少待满 retreatMinLiveDays 个回合，才允许做出撤退 / 回避决定。
            // （只影响非玩家控制的部队——本方法对 IsPlayerControl 已在开头 return）
            bool canDecideRetreat = aiConfig.retreatMinLiveDays <= 0 || liveDays > aiConfig.retreatMinLiveDays;

            // 【分级策略】战场态势不利时主动脱离接触：
            // 撤退概率完全由当前态势档位决定
            // （默认：危局 80% / 劣势 40% / 均势 10% / 优势与碾压 0%，均可在 AIConfig 中调整）
            if (canDecideRetreat && aiConfig.useTierStrategy && aiConfig.useTierRetreat)
            {
                TroopTierWeights tierWeights = GetTierWeights(scenario);
                if (tierWeights != null)
                {
                    TroopBattleTier tier = EvaluateTier(scenario);
                    int retreatChance = tierWeights.retreatChance;

                    // 【领队性格】叠加性格的撤退倾向（莽撞 −20% / 胆小 +20%）。
                    // 安全阀：危局档忽略性格修正，防止"莽将送死"。
                    if (!(aiConfig.leaderIgnoreRetreatInCritical && tier == TroopBattleTier.Critical))
                        retreatChance += GetLeaderRetreatBonus();

                    if (retreatChance > 0 && GameRandom.Chance(Math.Min(100, retreatChance)))
                    {
                        City refuge = FindNearestFriendlyCity(scenario);

                        // 【修复】撤退目标就是部队当前所在的城池时（刚出城），撤退无意义：
                        // 若此时切任务，部队会被"召回本城"而在城池格上反复切换，表现为卡住。
                        // 此时直接跳过撤退判定，让它继续执行原任务。
                        if (refuge != null && (cell == null || cell.building != refuge))
                        {
                            SetMission(MissionType.TroopMovetoCity, refuge.Id);
                            NeedPrepareMission();
                            Sango.Log.Info($"{BelongForce?.Name}的[{Name}]战场态势不利({tier}),主动脱离接触前往{refuge.Name}!");
                            return;
                        }
                    }
                }
            }

            // 【P2】保存实力:非玩家控制的部队在断粮 / 兵力过低 / 士气崩溃时,主动放弃进攻任务。
            // 【优化】改成"就近赶赴己方城市 / 港口 / 关卡入驻补给",不再固定返回归属城,
            // 避免老家太远时部队在半路因断粮 / 兵力耗尽而覆灭。
            bool outOfFood = IsWithOutFood() == 1;
            bool tooFewTroops = troops < aiConfig.retreatMinTroops;
            // 兵力低于满编一定比例时同样视为"兵力过低"(需要 MaxTroops 有效)
            if (!tooFewTroops && MaxTroops > 0 && aiConfig.retreatTroopPercent > 0
                && troops * 100 < MaxTroops * aiConfig.retreatTroopPercent)
            {
                tooFewTroops = true;
            }
            bool lowMorale = morale <= aiConfig.retreatMinMorale;

            if (canDecideRetreat &&
                ((outOfFood && GameRandom.Chance(aiConfig.retreatOutOfFoodChance)) ||
                 (tooFewTroops && GameRandom.Chance(aiConfig.retreatFewTroopsChance)) ||
                 (lowMorale && GameRandom.Chance(aiConfig.retreatLowMoraleChance))))
            {
                // 就近选择己方据点(城市 / 港口 / 关卡)入驻补给
                City nearestCity = FindNearestFriendlyCity(scenario);
                if (nearestCity == null)
                    return;

                // 【修复】与态势撤退同理：若"最近的己方据点"就是部队脚下的城池
                // （刚出城 / 已站在本城格上），切任务只会让它原地回城，
                // 在城池格上反复进出。这里跳过撤退，让它继续执行原任务。
                if (cell != null && cell.building == nearestCity)
                    return;

                // 使用 TroopMovetoCity(允许任意己方据点)而不是 TroopReturnCity(其完成判定限定归属城),
                // 这样不会改动通用返城任务的语义,玩家部队不受影响。
                SetMission(MissionType.TroopMovetoCity, nearestCity.Id);
                NeedPrepareMission();
                Sango.Log.Info($"{BelongForce?.Name}的[{Name}]兵力不足或断粮,就近赶赴{nearestCity.Name}补给!");
                return;
            }

            // 【前线建筑 / B3 随军增筑】以上撤退判定全部未触发（说明这支部队确实要继续作战），
            // 此时再看是否该在战场就地修一座辅助建筑。
            // 放在最后是有意为之：态势不利 / 断粮 / 兵少的部队应当先撤，而不是留下来花钱施工。
            if (aiConfig.useFieldBuilding)
                TryBuildFieldBuilding(scenario);
        }

        /// <summary>
        /// 【前线建筑 / B3 随军增筑】前线作战部队在战场就地修建战略辅助建筑（军乐台 / 砦 / 箭楼等）。
        ///
        /// 与专职工程队（由 <c>CityAI.DispatchBuildingTroop</c> 派遣）互补：
        ///   · 工程队：由城池成批派遣，负责成片、有计划的建设；
        ///   · 随军增筑：作战部队在战场态势刚变化时（如鏖战后气力枯竭、粮草将尽）
        ///     快速就地补建，无需等待后方工程队长途赶来。
        ///
        /// 触发条件（需全部满足）：
        ///   1. 开启了 <c>useFieldBuilding</c>
        ///   2. 当前为"作战类"任务（返城 / 施工 / 补给 / 求援等任务一律跳过，避免打断既定流程）
        ///   3. 兵力不低于满编的 <c>fieldBuildMinHealthPercent</c>%（濒临溃败先保命）
        ///   4. 携带资金足以修建至少一座候选建筑（没钱无法施工）
        ///   5. 本回合随机命中 <c>fieldBuildChance</c>%（避免频繁打断主线任务）
        ///   6. 附近存在评分达标的建址（<see cref="BattleSituation.EvaluateFrontSite"/>）
        ///
        /// 切换施工任务前会把原任务记录在 <see cref="fieldBuildReturnMission"/> /
        /// <see cref="fieldBuildReturnTarget"/>，建筑完工后由 <c>TroopBuildBuilding</c> 恢复，
        /// 因此作战部队不会因顺手修一座建筑而永久脱离战线。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否已切换到就地施工任务</returns>
        public bool TryBuildFieldBuilding(Scenario scenario)
        {
            AIConfig cfg = AIConfig.Instance;
            if (!cfg.useFieldBuilding || scenario == null || scenario.Map == null
                || cell == null || BelongForce == null)
                return false;

            // 只在"作战类"任务下触发，避免打断返城 / 施工 / 补给 / 求援等既定流程
            switch ((MissionType)missionType)
            {
                case MissionType.TroopOccupyCity:
                case MissionType.TroopDestroyTroop:
                case MissionType.TroopDestroyBuilding:
                case MissionType.TroopProtectCity:
                case MissionType.TroopBanishTroop:
                case MissionType.TroopStay:
                    break;
                default:
                    return false;
            }

            // 濒临溃败的部队优先保命，不施工
            if (MaxTroops > 0 && cfg.fieldBuildMinHealthPercent > 0
                && troops * 100 < MaxTroops * cfg.fieldBuildMinHealthPercent)
                return false;

            List<BuildingType> candidates = BelongForce.canBuildMilitaryBuildingType;
            if (candidates == null || candidates.Count == 0)
                return false;

            // 资金不足最便宜的一座建筑 → 无从施工
            int minCost = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                BuildingType t = candidates[i];
                if (t != null && t.cost < minCost)
                    minCost = t.cost;
            }
            if (minCost == int.MaxValue || gold < minCost || gold < cfg.fieldBuildMinGold)
                return false;

            // 概率触发：避免作战部队每回合都跑去盖房子
            if (cfg.fieldBuildChance <= 0 || !GameRandom.Chance(cfg.fieldBuildChance))
                return false;

            // 以自身所在格为中心评选建址
            BattleSituation.FrontSiteInfo best = new BattleSituation.FrontSiteInfo();
            best.Clear();
            int range = Math.Max(1, cfg.frontBuildSearchRange);
            scenario.Map.SpiralAction(cell, range, (c) =>
            {
                BattleSituation.FrontSiteInfo info =
                    BattleSituation.EvaluateFrontSite(c, BelongForce, scenario);
                if (!info.isValid || info.cell == null)
                    return;
                if (best.cell == null || info.score > best.score)
                    best = info;
            });

            if (!best.isValid || best.cell == null)
                return false;

            BuildingType buildingType = BattleSituation.SelectFrontBuildingType(best, this, BelongForce, scenario);
            if (buildingType == null || gold < buildingType.cost)
                return false;

            // 记录原任务，建筑完工后恢复（由 TroopBuildBuilding 的回城分支处理）
            fieldBuildReturnMission = (int)missionType;
            fieldBuildReturnTarget = missionTarget;

            missionTargetCell = best.cell;
            SetMission(MissionType.TroopBuildBuilding, buildingType.Id);
            NeedPrepareMission();
            Sango.Log.Info($"{BelongForce?.Name}的[{Name}]就地增筑{buildingType.Name}!");
            return true;
        }

        /// <summary>
        /// 查找距离本部队最近的己方据点(城市 / 港口 / 关卡)。
        /// 用于缺粮、兵力过低或士气崩溃时就近入驻补给。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>最近的可入驻据点,没有则返回 null</returns>
        public City FindNearestFriendlyCity(Scenario scenario)
        {
            if (scenario == null || scenario.citySet == null)
                return null;

            Cell selfCell = cell;
            if (selfCell == null)
                return null;

            City nearest = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive)
                    continue;
                // 必须是己方据点
                if (!city.IsSameForce(this))
                    continue;
                // 只考虑城市 / 港口 / 关卡
                if (!city.IsCity() && !city.IsPort() && !city.IsGate())
                    continue;

                int distance = scenario.Map.Distance(selfCell, city.CenterCell);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = city;
                }
            }
            return nearest;
        }

        /// <summary>
        /// 领队性格（无领队或性格数据缺失时为 null）。
        /// </summary>
        public Personality LeaderPersonality
        {
            get { return Leader != null ? Leader.mPersonality : null; }
        }

        /// <summary>
        /// 依据当前任务、自身属性与**领队性格**推导部队角色。
        ///
        /// 仅在 <see cref="role"/> 为 <see cref="TroopRole.Auto"/> 时调用一次并固化，
        /// 避免每回合重算导致行为抖动。
        /// </summary>
        /// <returns>推导出的角色</returns>
        public TroopRole ResolveRole()
        {
            // 运输 / 补给队一律按护卫处理
            if (IsTransport)
                return TroopRole.Escort;

            // 修建 / 维修建筑 → 工兵。
            // 【注意】这里必须直接 return，不能走后面的"领队性格覆盖"分支：
            // 工程队的职责是把前线辅助建筑修起来，若被好战领队覆盖成攻坚角色，
            // 它会丢下施工任务去正面接战，导致工程队形同虚设。
            if (missionType == (int)MissionType.TroopBuildBuilding
                || missionType == (int)MissionType.TroopFixBuilding)
                return TroopRole.Engineer;

            // ---------- 先按任务推导 ----------
            TroopRole missionRole = TroopRole.Auto;
            switch ((MissionType)missionType)
            {
                // 攻打城池 / 建筑 → 攻坚
                case MissionType.TroopOccupyCity:
                case MissionType.TroopDestroyBuilding:
                    missionRole = TroopRole.Assault;
                    break;

                // 守卫据点 / 友军 → 防守
                case MissionType.TroopProtectCity:
                case MissionType.TroopProtectBuilding:
                case MissionType.TroopProtectTroop:
                    missionRole = TroopRole.Defender;
                    break;

                // 补给 / 求援 → 护卫
                case MissionType.TroopSupplyTroop:
                case MissionType.TroopAskSupply:
                    return TroopRole.Escort;

                // 移动 / 待命 → 守备
                case MissionType.TroopReturnCity:
                case MissionType.TroopMovetoCity:
                case MissionType.TroopMovetoCell:
                case MissionType.TroopMovetoBuild:
                case MissionType.TroopStay:
                    return TroopRole.Guard;
            }

            // ---------- 再按领队性格推导并加权覆盖 ----------
            // 【分级策略】性格倾向越强越容易覆盖任务：好战指数达到阈值即覆盖任务角色
            AIConfig cfg = AIConfig.Instance;
            if (cfg.useLeaderPersonality && cfg.useLeaderRoleOverride)
            {
                Personality personality = LeaderPersonality;
                // preferTroopRole = -1 表示"性格不指定角色"。
                // 旧实现用 troopAggression 的符号猜（>0 攻坚 / <0 防守），
                // 相当于把数值隐含成枚举，新增"中立好战"这类性格时无法表达；
                // 现在由性格数据直接声明倾向角色，代码只负责"要不要覆盖"。
                if (personality != null && personality.preferTroopRole >= 0)
                {
                    int strength = Math.Abs(personality.troopAggression);
                    // 性格允许的覆盖阈值（性格自身可再微调）
                    int threshold = cfg.leaderRoleOverrideThreshold + personality.troopRoleOverrideAdd;
                    if (threshold < 0)
                        threshold = 0;

                    TroopRole leaderRole = (TroopRole)personality.preferTroopRole;

                    // 倾向强烈 → 覆盖任务；否则以任务为准；任务无法映射时直接采用性格角色
                    if (missionRole == TroopRole.Auto || strength >= threshold)
                        return leaderRole;
                }
            }

            if (missionRole != TroopRole.Auto)
                return missionRole;

            // ---------- 任务与性格均无法判定：按自身属性细分 ----------
            // 兵力低于满编一半 → 游击（保存实力、择机而动）
            if (MaxTroops > 0 && troops * 2 < MaxTroops)
                return TroopRole.Skirmisher;

            // 骑兵适应突出 → 骚扰（高机动，适合打了就跑）
            if (RideLv >= 2)
                return TroopRole.Harasser;

            // 其余按攻坚处理
            return TroopRole.Assault;
        }

        /// <summary>
        /// 取得本部队当前生效的角色权重。角色分级关闭或角色未定时返回 null（表示不做角色修正）。
        /// </summary>
        /// <returns>角色权重，无则返回 null</returns>
        public TroopRoleWeights GetRoleWeights()
        {
            AIConfig cfg = AIConfig.Instance;
            if (!cfg.useTroopRole)
                return null;

            switch (role)
            {
                case TroopRole.Assault: return cfg.roleAssault;
                case TroopRole.Defender: return cfg.roleDefender;
                case TroopRole.Harasser: return cfg.roleHarasser;
                case TroopRole.Skirmisher: return cfg.roleSkirmisher;
                case TroopRole.Escort: return cfg.roleEscort;
                case TroopRole.Guard: return cfg.roleGuard;
                case TroopRole.Engineer: return cfg.roleEngineer;
            }
            return null;
        }

        /// <summary>
        /// 评估本部队当前所处的战场态势档位（基于局部敌我兵力对比）。
        ///
        /// 【性能说明】每次调用会扫描周围 <c>tierScanRange</c> 格内的部队，
        /// 因此建议每回合评估一次并缓存结果，不要在循环中反复调用。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>态势档位；未启用或无法评估时返回 Even（均势）</returns>
        public TroopBattleTier EvaluateTier(Scenario scenario)
        {
            AIConfig cfg = AIConfig.Instance;
            if (!cfg.useTierStrategy || scenario == null || cell == null)
                return TroopBattleTier.Even;

            BattleSituation.BalanceSnapshot balance =
                BattleSituation.EvaluateBalance(cell, BelongForce, cfg.tierScanRange, scenario);
            return BattleSituation.GetTier(balance.balancePercent);
        }

        /// <summary>
        /// 取得当前态势档位对应的权重。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>档位权重</returns>
        public TroopTierWeights GetTierWeights(Scenario scenario)
        {
            // 【委任 AI】委任部队不评估战场态势：返回 null 表示"不做档位修正"。
            // 这是一处"总闸"——所有态势相关调用（技能评分倍率、行动择优池、
            // 主动撤退等）都经过本方法，在这里拦掉即可保证委任部队
            // 在任何任务下都不会因战场形势而改变既定行为。
            if (AIType == TroopAIType.Appoint)
                return null;

            return AIConfig.Instance.GetTierWeights(EvaluateTier(scenario));
        }

        /// <summary>
        /// 领队性格给出的撤退概率修正（%，正值更倾向撤退）。
        /// </summary>
        /// <returns>撤退概率修正；未启用或性格缺失时返回 0</returns>
        public int GetLeaderRetreatBonus()
        {
            if (!AIConfig.Instance.useLeaderPersonality)
                return 0;
            Personality PersonalityId = LeaderPersonality;
            return PersonalityId != null ? PersonalityId.troopRetreatAdd : 0;
        }

        public void Burn(Cell dest)
        {

        }

        public int GetItemNumber(int itemKind)
        {
            if (IsTransport)
                return itemStore.GetNumber(itemKind);
            else
            {
                int number = troops + woundedTroops;
                if (LandTroopType.costItems != null && LandTroopType.costItems.Length > 0)
                {
                    for (int i = 0; i < LandTroopType.costItems.Length; i += 2)
                    {
                        int itemTypeId = LandTroopType.costItems[i];
                        if (itemKind == itemTypeId)
                            return LandTroopType.costItems[i + 1] * number / 1000;
                    }
                }

                if (WaterTroopType.costItems != null && WaterTroopType.costItems.Length > 0)
                {
                    for (int i = 0; i < WaterTroopType.costItems.Length; i += 2)
                    {
                        int itemTypeId = WaterTroopType.costItems[i];
                        if (itemKind == itemTypeId)
                            return WaterTroopType.costItems[i + 1] * number / 1000;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// 添加囚犯
        /// </summary>
        /// <param name="person">要添加的武将</param>
        /// <returns>添加的武将</returns>
        public Person AddCaptive(Person person)
        {
            if (person == null)
                return null;

            // 规则：势力尚存的君主不可被俘虏(收押/登用)，只能被"释放"或"斩首"。
            // 用 IsActiveGovernor：势力已灭亡的君主下野后是普通在野武将，可被收押/招募。
            if (person.IsActiveGovernor)
            {
                Sango.Log.Error($"*{Name} -> 拒绝收押君主 {person.Name}（势力尚存，只能被释放或斩首）");
                return null;
            }

            // 【归属治理】部队收押统一走 PersonStateSystem.ToCaptive（对应 Open311 的 System::capture）。
            // 原先这里与 City.AddCaptive 各写一套"改 state + 摘名册 + 置空/赋值部队字段"，
            // 两处口径稍有差异就会产生"人在俘虏名单里却还是 Normal""旧城名册留着脏引用"。
            PersonStateSystem.ToCaptive(person, CurrentCity, PersonChangeReason.Captured, this);

            Sango.Log.Info($"@人才@[{person.Name}]被<{BelongForce?.Name}>俘虏至{Name}");
            return person;
        }

        /// <summary>
        /// 添加囚犯
        /// </summary>
        /// <param name="person">要添加的武将</param>
        /// <returns>添加的武将</returns>
        public Person RemoveCaptive(Person person)
        {
            Sango.Log.Info($"*{Name} -> captiveList 删除 {person.Name} ");
            captiveList.Remove(person);
            person.BelongForce?.BeCaptiveList.Remove(person);
            person.mBelongTroop = null;
            return person;
        }

        /// <summary>
        /// 结算部队本次战斗获得的功绩、能力经验、兵种适性经验与势力技巧点。
        ///
        /// 【原版对齐】四个量全部来自 Exp 表同一行（<see cref="CombatExpTable"/>），各取各的字段：
        /// <list type="bullet">
        /// <item>功绩 <c>kouseki</c> —— s11_sys_scen_unit.cpp:1423 <c>unit_add_kouseki(self, exp->kouseki)</c></item>
        /// <item>能力经验 <c>stat_exp[5]</c> —— :1380-1389 <c>unit_add_exp</c></item>
        /// <item>适性经验 <c>heishu_exp[6]</c> —— :1391-1402 <c>unit_add_equipment_exp</c></item>
        /// <item>技巧点 <c>tech_point</c> —— :1425 <c>return exp->tech_point * mul</c></item>
        /// </list>
        /// 原版对全队每个存活武将给**同一份**数值，不区分主将 / 副将；
        /// 因此这里取消了旧版的 MeritLeaderFactor / MeritMemberFactor 折扣。
        /// </summary>
        /// <param name="expEvent">本次结算对应的事件行（普攻 / 战法命中 / 计略 / 陷阱…）</param>
        /// <param name="isDestroyEnemyTroop">
        /// 是否由本部队击破敌方部队触发。原版击破时额外给全队每人 +300 功绩与势力 +50 技巧点：
        /// s11_sys_scen_unit.cpp:340-350、:460-483。
        /// </param>
        /// <param name="techniqueMultiplier">
        /// 技巧点倍数（计略用命中目标数）。原版 <c>unit_add_exp_with_kouseki(..., mul, ...)</c>
        /// 返回 <c>exp-&gt;tech_point * mul</c>（s11_sys_scen_unit.cpp:1425），
        /// 但**功绩不乘** —— 功绩在函数内部按原值加给每个 member。
        /// </param>
        public void GainEP(CombatExpEvent expEvent, bool isDestroyEnemyTroop = false, int techniqueMultiplier = 1)
        {
            CombatExpEntry entry = CombatExpTable.Get(expEvent);

            // ---- 功绩：全队每人一份（原版 unit_add_kouseki 内部就是对每个 member 调 person_add_kouseki）----
            int merit = entry.merit;
            if (isDestroyEnemyTroop)
                merit += GainValueConfig.Merit(GainPlace.CombatDestroyTroop);

            if (merit > 0)
            {
                // 武将等级经验是项目自有系统（原版 Person 只有功绩没有等级），
                // 这里保留"按功绩折算"的口径，不影响上面三项的原版对齐。
                int exp = GainValueConfig.Rate(merit, GainValueConfig.Exp(GainPlace.ExpFromMerit));
                ForEachPerson(x =>
                {
                    x.GainMerit(merit);
                    if (exp > 0) x.GainExp(exp);
                });
            }

            // ---- 能力经验：stat_exp[5]，按原版 PersonStatType 顺序（0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力）----
            if (entry.leadership > 0 || entry.strength > 0 || entry.intelligence > 0
                || entry.politics > 0 || entry.charisma > 0)
            {
                ForEachPerson(x =>
                {
                    x.GainStatExp(0, entry.leadership);
                    x.GainStatExp(1, entry.strength);
                    x.GainStatExp(2, entry.intelligence);
                    x.GainStatExp(3, entry.politics);
                    x.GainStatExp(4, entry.charisma);
                });
            }

            // ---- 适性经验：只取"当前兵种"那一格（原版 get_equipment(id)->get_heishu() 取一项）----
            int influenceAbility = TroopType != null ? TroopType.influenceAbility : 0;
            int abilityExp = GetHeishuExp(entry, influenceAbility);
            if (abilityExp > 0)
            {
                ForEachPerson(x => x.GainAbilityExp(influenceAbility, abilityExp));
            }

            // ---- 势力技巧点：tech_point × 命中数 + 击破固定值；「名声」翻倍交给特技 Action 改写 ----
            int rawTechniquePoint = entry.techniquePoint;
            if (techniqueMultiplier > 1)
                rawTechniquePoint *= techniqueMultiplier;
            if (isDestroyEnemyTroop)
                rawTechniquePoint += GainValueConfig.TechniquePoint(GainPlace.TechniquePointDestroyTroop);

            if (rawTechniquePoint > 0 && BelongForce != null)
            {
                Tools.OverrideData<int> techniquePoint = Tools.OverrideData<int>.Create(rawTechniquePoint);
                // 由已装配的部队特技决定是否改写本次技巧点，避免按特技 ID 硬编码。
                GameEvent.OnTroopCalculateTechniquePoint?.Invoke(this, isDestroyEnemyTroop, techniquePoint);
                BelongForce.GainTechniquePoint(techniquePoint.ValueAndRecycle);
            }
        }

        /// <summary>
        /// 按部队当前兵种取 Exp 表的 heishu_exp 对应格。
        /// 原版 Heishu 顺序（s11_enum_scen.h:116-125）：枪 / 戟 / 弩 / 骑 / 兵器 / 水军；
        /// 项目 <see cref="AbilityType"/> 顺序：枪 / 戟 / 弩 / 骑 / 器械 / 水军，一一对应。
        /// </summary>
        static int GetHeishuExp(CombatExpEntry entry, int influenceAbility)
        {
            switch ((AbilityType)influenceAbility)
            {
                case AbilityType.Spear: return entry.spear;
                case AbilityType.Halberd: return entry.halberd;
                case AbilityType.Crossbow: return entry.bow;
                case AbilityType.Ride: return entry.cavalry;
                case AbilityType.Machine: return entry.siege;
                case AbilityType.Water: return entry.navy;
                default: return 0;
            }
        }

        public void GainTargetResource(Troop target)
        {
            // 获取对方部分钱粮
            int getFood = target.food * Math.Max(0, Math.Min(100, defeatTroopCanGainFoodFactor)) / 100;
            int getGold = target.gold * Math.Max(0, Math.Min(100, defeatTroopCanGainGoldFactor)) / 100;
            if (getFood > 0)
            {
                ChangeFood(getFood);
            }
            if (getGold > 0)
            {
                ChangeGold(getGold);
            }
        }

        public void FaceTo(Cell target)
        {
            if(cell == target || target == null) return;
            Render?.FaceTo(target.Position);
        }
    }
}
