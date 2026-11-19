using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UITroopHeadbar : UGUIWindow
    {
        public RawImage headIcon;
        public Text name;
        public Image state;
        public Image food;
        public Image energy;
        public Image angry;
        public Image bg;
        public Text number;
        public AnimationText skillText;
        public UIAnimationText aniText;
        public Troop troop;
        public Animation foodAni;

        /// <summary>
        /// 上一次写入底框的势力ID。
        /// 【刷新治理】Image.color 每次赋值都会标脏画布,而 UpdateState 的调用频率很高,
        /// 因此只在势力真的变了时才写一次。
        /// </summary>
        int lastForceId = int.MinValue;

        /// <summary>同盟标记图标键（4846-9 图集）</summary>
        const string AllianceStateIconKey = "4846-9/4846-9_20";

        /// <summary>停战标记图标键（4846-9 图集）</summary>
        const string TruceStateIconKey = "4846-9/4846-9_24";

        /// <summary>
        /// 当前写入 <see cref="state"/> 的图标键（如 "4846-9/4846-9_20"），
        /// 即 <see cref="GameRenderHelper.LoadTroopStateIcon"/> 的入参（带目录的相对路径）。
        ///
        /// 【为什么不比较 state.sprite.name】Sprite 的 name 只是资源文件名（"4846-9_20"），
        /// 这里要区分的是带目录的键，两者永远不相等 —— 每帧刷新都会重新走一遍
        /// 路径拼接、冒号拆分与资源查找，等于白白制造 GC。
        /// 改为记住键：键没变就跳过。
        /// 加载失败现在由 <see cref="GameRenderHelper.LoadTroopStateIcon"/> 回落 empty.png 占位图，
        /// 占位图也取不到时仍返回 null —— 那种情况下面的 sprite 判空会重新尝试加载。
        /// </summary>
        string lastStateSpriteKey;

        /// <summary>
        /// 写入部队状态图标，值没变就不写控件。
        /// sprite 为空（加载失败 / 池化回收清空）时会重新写一次。
        /// </summary>
        /// <param name="spName">图标相对路径键，如 "4846-9/4846-9_20"</param>
        void SetStateSprite(string spName)
        {
            if (state.sprite != null && lastStateSpriteKey == spName)
                return;

            lastStateSpriteKey = spName;
            state.sprite = GameRenderHelper.LoadTroopStateIcon(spName);
        }

        /// <summary>
        /// 刷新底框的势力色。
        ///
        /// 原先背景色只在 <see cref="Init"/> 里写一次,部队换势力(登庸带兵的武将)之后
        /// 血条底框会一直停留在旧势力色上,直到模型重新加载(headbar 随之重建)才纠正 ——
        /// 这正是"登庸成功、他头顶颜色没变化,下个回合才变回来"的直接原因。
        /// </summary>
        void UpdateForceColor()
        {
            Force force = troop != null ? troop.BelongForce : null;
            if (force == null) return;
            if (lastForceId == force.Id) return;
            lastForceId = force.Id;
            bg.color = force.Color;
        }

        public void Init(Troop troop)
        {
            aniText = null;
            this.troop = troop;
            lastForceId = int.MinValue;
            // headbar 是池化复用的：重置图标键，保证复用后第一次刷新必定写一次图标
            lastStateSpriteKey = null;
            name.text = troop.Name;
            headIcon.texture = GameRenderHelper.LoadHeadIcon(troop.Leader.headIconID);
            skillText.Clear();
            UpdateState(troop);
            GameEvent.OnForceTurnStart += OnForceStart;
        }

        public override void OnClose()
        {
            GameEvent.OnForceTurnStart -= OnForceStart;
        }

        void OnForceStart(Force force, Scenario scenario)
        {
            // 玩家回合,更新状态
            if (force.IsPlayer)
            {
                UpdateTroopState();
            }
        }

        public void UpdateTroopState()
        {
            if (troop.BelongForce == null) return;

            // 除开自己以外全部不显示
            if (troop.BelongForce.IsPlayer)
            {
                string spName;
                if(troop.missionType > 0)
                {
                    spName = troop.ActionOver ? "4846-9/4846-9_21" : "4846-9/4846-9_19";
                }
                else
                {
                    spName = troop.ActionOver ? "4846-9/4846-9_17" : "4846-9/4846-9_18";
                }

                SetStateSprite(spName);
                state.enabled = true;
            }
            else
            {
                // 只认"与玩家势力"的盟约。判定基准必须是 Force.IsPlayer，
                // 不能用 Scenario.Cur.CurRunForce —— 后者是"当前正在行动的势力"，
                // AI 回合里拿到的是 AI，会把 AI 与 AI 之间的盟约也画到部队头顶，
                // 玩家看到的就是一堆与自己无关的盟约标记。
                Alliance alliance = FindAllianceWithAnyPlayer(troop.BelongForce);
                if (alliance != null)
                {
                    // 按盟约类型区分图标：同盟 4846-9_20 / 停战 4846-9_24
                    SetStateSprite(alliance.allianceType == AllianceType.Truce
                        ? TruceStateIconKey
                        : AllianceStateIconKey);
                    state.enabled = true;
                }
                else
                {
                    state.enabled = false;
                }
            }
        }

        /// <summary>
        /// 找出某势力与任意一个玩家势力之间的盟约（同盟 / 停战）。
        ///
        /// 【为什么不用 Scenario.Cur.CurRunForce】
        /// CurRunForce 是"当前正在行动的势力"。AI 势力回合里它是那个 AI，
        /// 拿它当判定基准会把"两个 AI 之间的盟约"也当成玩家的盟约显示出来，
        /// 玩家在这些回合观战时就会看到一串本与自己无关的同盟或停战标记。
        /// 玩家势力的唯一权威口径是 <see cref="Force.IsPlayer"/>
        /// （由剧本 playerForceList 设定，中途参加 / 委托给AI 时由 ForceControlService
        /// 在回合边界改写），因此这里以它为基准。
        ///
        /// 玩家势力可能有多个（多人协作、中途参加），与其中任意一个结盟即视为盟军；
        /// 若同时存在同盟与停战两种盟约，优先返回同盟（图标语义更强，也更符合玩家预期）。
        /// 盟约两侧都会登记（AllianceAction / Truce / 事件效果都是双向 Add），
        /// 这里仍双向查一次 —— 与 <see cref="Sango.Core.CityStateSystem"/> 的处理口径一致，
        /// 防止老存档里只登记在单侧的盟约被漏判。
        /// 查询统一用带 <see cref="AllianceType"/> 的重载：它额外过滤 IsAlive 与 leftCount，
        /// 不会把已到期但尚未从列表摘除的盟约当成有效盟约。
        /// </summary>
        /// <param name="force">部队所属势力</param>
        /// <returns>命中的盟约；没有则返回 null</returns>
        static Alliance FindAllianceWithAnyPlayer(Force force)
        {
            Scenario scenario = Scenario.Cur;
            if (force == null || scenario == null || scenario.forceSet == null)
                return null;

            Alliance foundTruce = null;
            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force player = scenario.forceSet[i];
                // 已灭亡的势力不作为判定基准：它的 AllianceList 里可能还挂着尚未清理的盟约
                if (player == null || !player.IsAlive || !player.IsPlayer || player == force)
                    continue;

                // 同盟优先：命中即刻返回，不再看后面的势力
                Alliance alliance = force.CheckAlliance(player, AllianceType.Alliance)
                                 ?? player.CheckAlliance(force, AllianceType.Alliance);
                if (alliance != null)
                    return alliance;

                // 停战只做记录，等确认没有同盟之后再返回
                if (foundTruce == null)
                    foundTruce = force.CheckAlliance(player, AllianceType.Truce)
                              ?? player.CheckAlliance(force, AllianceType.Truce);
            }

            return foundTruce;
        }

        // 【刷新治理】值没变就不写控件：
        // Text.text 赋值会触发 Mesh 重建，fillAmount 也会标脏，而本方法在表现层刷新里调用很频繁。
        int lastStateTroops = int.MinValue;
        int lastStateMorale = int.MinValue;
        bool lastStateFood;
        bool lastStateFoodInited;

        public void UpdateState(Troop troop)
        {
            // 先看势力有没有换:换旗后 UI 的归属色与状态图标口径都要跟着变
            UpdateForceColor();

            // 原 TODO（拆开刷新）先把"值没变就不写"这一步做掉，收益最直接
            UpdateTroopState();

            if (lastStateMorale != troop.morale)
            {
                lastStateMorale = troop.morale;
                energy.fillAmount = (float)troop.morale / troop.MaxMorale;
            }
            angry.fillAmount = 0;

            if (lastStateTroops != troop.troops)
            {
                lastStateTroops = troop.troops;
                number.text = troop.troops.ToString();
            }

            bool isWithoutFood = troop.IsWithOutFood() <= 1;
            if (!lastStateFoodInited || lastStateFood != isWithoutFood)
            {
                lastStateFoodInited = true;
                lastStateFood = isWithoutFood;
                food.enabled = isWithoutFood;
                if (isWithoutFood)
                {
                    foodAni.Play();
                }
                else
                {
                    foodAni.Stop();
                }
            }
        }

        public void ShowInfo(int damage, int damageType = 0)
        {
            aniText = UIAnimationText.Show(aniText, troop, damage, damageType);
            if (aniText != null)
            {
                aniText.onAnimationComplate = OnAnimationComplate;
            }
        }

        public void ShowInfo(int damage, bool isCrit, int damageType = 0)
        {
            aniText = UIAnimationText.Show(aniText, troop, damage, damageType, isCrit);
            if (aniText != null)
            {
                aniText.onAnimationComplate = OnAnimationComplate;
            }
        }

        void OnAnimationComplate()
        {
            aniText = null;
        }

        public void ShowSkill(SkillInstance skill, bool isFail, bool isCritical)
        {
            if (isFail)
            {
                skillText.flipY = true;
                skillText.Create(skill.Name + "(失败)", UnityEngine.Color.gray, 1f);
            }
            else
            {
                if (isCritical)
                {
                    skillText.flipY = false;
                    skillText.Create(skill.Name + "<暴击!!>", UnityEngine.Color.red, 1f);
                }
                else
                {
                    skillText.flipY = false;
                    skillText.Create(skill.Name, UnityEngine.Color.green, 1f);
                }
            }
        }
    }
}
