using Sango.Loader;
using UnityEngine;
using UnityEngine.UI;
using Sango.Core; namespace Sango.UI

{
    public class UIBuildingBaseHeadbar : UGUIWindow
    {
        public Text name;
        public Image durability;
        public UIAnimationText aniText;
        public BuildingBase building;

        GameObject durabilityObj;

        // ---------- 与玩家的同盟 / 停战标记 ----------

        /// <summary>同盟标记图标键（4846-9 图集，与部队头顶同一套图）</summary>
        protected const string AllianceStateIconKey = "4846-9/4846-9_20";

        /// <summary>停战标记图标键（4846-9 图集，与部队头顶同一套图）</summary>
        protected const string TruceStateIconKey = "4846-9/4846-9_24";

        /// <summary>
        /// 上一次写入的盟约图标键（如 "4846-9/4846-9_20"）。
        ///
        /// 【为什么要判重】headbar 是对象池复用的，且 <see cref="UpdateState"/> 的调用频率很高；
        /// Image.sprite 每次赋值都会标脏画布，重复写同一个图标纯属浪费。
        /// 只比键不比 sprite.name —— 后者只是资源文件名（"4846-9_20"），永远不等于带目录的键。
        /// </summary>
        string lastAllianceStateKey;

        protected override void Awake()
        {
            durabilityObj = durability.transform.parent.gameObject;
        }

        public virtual void Init(BuildingBase building)
        {
            aniText = null;
            this.building = building;
            // 池化复用：重置判重键，保证复用后的第一次刷新必定写一次图标
            lastAllianceStateKey = null;
            if (name != null)
                name.text = building.Name;
            UpdateState(building);
        }

        public virtual void UpdateState(BuildingBase building)
        {
            if (durability == null) return;
            durability.fillAmount = (float)building.durability / building.DurabilityLimit;
            if(building.IsIntorBuilding())
            {
                durabilityObj.SetActive(durability.fillAmount < 1);
            }
            else
            {
                durabilityObj.SetActive(true);
            }
        }

        /// <summary>
        /// 刷新"与玩家的同盟 / 停战"标记（显示到 <paramref name="stateImage"/> 上）。
        ///
        /// 【口径与部队头顶完全一致】（见 <see cref="UITroopHeadbar.UpdateTroopState"/>）：
        ///   · 玩家自己的城 / 设施不显示 —— 自己不会与自己结盟；
        ///   · 判定基准必须是 <see cref="Force.IsPlayer"/>，不能用 Scenario.Cur.CurRunForce ——
        ///     后者是"当前正在行动的势力"，AI 回合里拿到的是 AI，会把 AI 与 AI 之间的盟约
        ///     也画到地图上，玩家看到的是一堆与自己无关的标记；
        ///   · 与任意一个玩家势力有盟约就显示：同盟（4846-9_20）优先于停战（4846-9_24）。
        /// </summary>
        /// <param name="stateImage">prefab 上预留的状态图标位（可为 null）</param>
        /// <param name="building">当前头顶条对应的城 / 关 / 港 / 设施</param>
        protected void UpdateAllianceState(Image stateImage, BuildingBase building)
        {
            if (stateImage == null)
                return;

            Force force = building != null ? building.BelongForce : null;
            if (force == null || force.IsPlayer)
            {
                HideAllianceState(stateImage);
                return;
            }

            Alliance alliance = FindAllianceWithAnyPlayer(force);
            if (alliance == null)
            {
                HideAllianceState(stateImage);
                return;
            }

            string key = alliance.allianceType == AllianceType.Truce ? TruceStateIconKey : AllianceStateIconKey;

            // sprite 为空说明还没写过（或池化回收时被清空 / 上次加载失败），必须重写一次
            if (stateImage.sprite == null || lastAllianceStateKey != key)
            {
                lastAllianceStateKey = key;
                stateImage.sprite = GameRenderHelper.LoadTroopStateIcon(key);
            }

            // 城市 bar 的 state 节点在 prefab 里是 m_IsActive: 0（建筑 bar 是 1），
            // 只写 Image.enabled 对未激活的节点无效 —— 必须先保证节点本身是打开的
            if (!stateImage.gameObject.activeSelf)
                stateImage.gameObject.SetActive(true);

            stateImage.enabled = true;
        }

        /// <summary>
        /// 隐藏盟约标记。已经隐藏且没有缓存键时直接返回，避免高频刷新反复写 <c>enabled</c>。
        /// </summary>
        void HideAllianceState(Image stateImage)
        {
            if (!stateImage.enabled && lastAllianceStateKey == null)
                return;

            stateImage.enabled = false;
            lastAllianceStateKey = null;
        }

        /// <summary>
        /// 找出某势力与任意一个玩家势力之间的盟约（同盟 / 停战）。
        ///
        /// 【为什么不用 Scenario.Cur.CurRunForce】CurRunForce 是"当前正在行动的势力"，
        /// 拿它当判定基准会把"两个 AI 之间的盟约"也当成玩家的盟约显示出来。
        /// 玩家势力的唯一权威口径是 <see cref="Force.IsPlayer"/>。
        ///
        /// 玩家势力可能有多个（多人协作 / 中途参加），与其中任意一个结盟即视为盟军；
        /// 若同时存在同盟与停战两种盟约，优先返回同盟（图标语义更强）。
        /// 盟约两侧都会登记，这里仍双向查一次 —— 防止老存档里只登记在单侧的盟约被漏判。
        /// 查询统一用带 <see cref="AllianceType"/> 的重载：它额外过滤 IsAlive 与 leftCount，
        /// 不会把已到期但尚未从列表摘除的盟约当成有效盟约。
        /// </summary>
        /// <param name="force">城 / 设施所属势力</param>
        /// <returns>命中的盟约；没有则返回 null</returns>
        protected static Alliance FindAllianceWithAnyPlayer(Force force)
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

        public virtual void ShowInfo(int damage, int damageType = 13)
        {
            aniText = UIAnimationText.Show(aniText, building, damage, damageType);
            if(aniText != null)
            {
                aniText.onAnimationComplate = OnAnimationComplate;
            }
        }
        void OnAnimationComplate()
        {
            aniText = null;
        }
    }
}
