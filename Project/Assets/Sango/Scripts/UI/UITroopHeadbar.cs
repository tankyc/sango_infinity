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

                if (state.sprite == null || !state.sprite.name.Equals(spName))
                    state.sprite = GameRenderHelper.LoadTroopStateIcon(spName);
                state.enabled = true;
            }
            else
            {
                Alliance alliance = troop.BelongForce.CheckAlliance(Scenario.Cur.CurRunForce);
                if (alliance != null)
                {
                    //TODO: 根据联盟类型显示(同盟或者停战)
                    string spName = "4846-9/4846-9_20";
                    if (state.sprite == null || !state.sprite.name.Equals(spName))
                        state.sprite = GameRenderHelper.LoadTroopStateIcon(spName);
                    state.enabled = true;
                }
                else
                {
                    state.enabled = false;
                }
            }
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
