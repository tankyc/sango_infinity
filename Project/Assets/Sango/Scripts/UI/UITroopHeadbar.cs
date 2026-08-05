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
        public void Init(Troop troop)
        {
            aniText = null;
            this.troop = troop;
            name.text = troop.Name;
            headIcon.texture = GameRenderHelper.LoadHeadIcon(troop.Leader.headIconID);
            bg.color = troop.BelongForce.Color;
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

        public void UpdateState(Troop troop)
        {
            //TODO: 这里需要拆开刷新,增加刷新标记后在Update刷新
            UpdateTroopState();
            energy.fillAmount = (float)troop.morale / troop.MaxMorale;
            angry.fillAmount = 0;
            number.text = troop.troops.ToString();
            bool isWithoutFood = troop.IsWithOutFood() <= 1;
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
