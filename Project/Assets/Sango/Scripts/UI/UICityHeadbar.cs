using System.Text;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityHeadbar : UIBuildingBaseHeadbar
    {
        public static bool showIndo = false;
        public Image state;
        public Image food;
        public Image bg;
        public Text number;
        public Text info;
        public UnityEngine.GameObject corpsNode;
        public Text corpsText;
        public Image corpsImg;
        public Image boder;

        /// <summary>玩家自己边境城市的边框色 #FF5151（红）</summary>
        static readonly Color PlayerBorderColor = new Color32(0xFF, 0x51, 0x51, 0xFF);

        /// <summary>与玩家同盟 / 停战势力的城市边框色 #00E6FF（蓝）</summary>
        static readonly Color AllyBorderColor = new Color32(0x00, 0xE6, 0xFF, 0xFF);

        public override void UpdateState(BuildingBase building)
        {
            base.UpdateState(building);
            City city = (City)building;
            // 与玩家的同盟 / 停战标记：玩家自己的城不显示，判定基准与部队头顶一致
            UpdateAllianceState(state, building);
            food.enabled = false;
            number.text = city.troops.ToString();
            if (info != null)
                info.enabled = showIndo;
            string cityInfo = $"(人:{city.allPersons.Count}闲:{city.freePersons.Count})俘虏:{city.captiveList.Count},建:{city.GetInteriorCellUsedCount()}/{city.InteriorCellCount}\n[商:{city.commerce},农:{city.agriculture}治:{city.security},训:{city.morale}]\n<金:{city.gold}+{city.totalGainGold}><粮:{city.food}+{city.totalGainFood}>\n枪:{city.itemStore.GetNumber(2)},刀:{city.itemStore.GetNumber(3)}驽:{city.itemStore.GetNumber(4)},马:{city.itemStore.GetNumber(5)}\n船:{city.itemStore.GetNumber(9)},冲车:{city.itemStore.GetNumber(7)}井阑:{city.itemStore.GetNumber(8)}";
            if (city.IsBorderCity)
                cityInfo = $"*{cityInfo}";
            info.text = cityInfo;

            bg.enabled = building.BelongForce != null;
            // 边框：玩家自己的边境城（红） / 同盟·停战势力的城池（蓝）
            UpdateBorder(city);

            if (building.BelongForce != null)
            {
                bg.color = building.BelongForce.Color;
            }

            Corps corps = building.BelongCorps;
            if (corps != null && corps.IsPlayer && corps.Index > 1)
            {
                corpsNode.SetActive(true);
                corpsImg.color = building.BelongCorps.Color;
                corpsText.text = building.BelongCorps.Index.ToString();
            }
            else
            {
                corpsNode.SetActive(false);
            }

        }

        /// <summary>
        /// 刷新城池边框（<see cref="boder"/>）。
        ///
        /// 【口径】
        ///   · 玩家自己的边境城市 → #FF5151（红框）；
        ///   · 与玩家同盟 / 停战势力的城市 → #00E6FF（蓝框）；
        ///   · 其余势力（敌对 / 中立）以及玩家的非边境城市 → 不显示。
        ///
        /// 玩家判定基准用 <see cref="Force.IsPlayer"/>，与头顶盟约标记
        /// （<see cref="UIBuildingBaseHeadbar.UpdateAllianceState"/>）同一口径 ——
        /// 不能用 CurRunForce：AI 回合里它指向 AI，会把 AI 之间的盟约也画出来。
        /// </summary>
        /// <param name="city">当前头顶条对应的城池</param>
        void UpdateBorder(City city)
        {
            if (boder == null)
                return;

            Force force = city.BelongForce;
            bool isPlayer = force != null && force.IsPlayer;

            // 玩家：只框边境城市（原行为，颜色改为红）
            bool showPlayerBorder = isPlayer && city.IsCity() && city.IsBorderCity;

            // 同盟 / 停战势力：城池全部框出（不要求是边境）
            bool showAllyBorder = !isPlayer && force != null && city.IsCity()
                                  && FindAllianceWithAnyPlayer(force) != null;

            bool show = showPlayerBorder || showAllyBorder;
            if (boder.enabled != show)
                boder.enabled = show;

            if (!show)
                return;

            Color target = showPlayerBorder ? PlayerBorderColor : AllyBorderColor;
            // Image.color 赋值会标脏画布，值没变就不写
            if (boder.color != target)
                boder.color = target;
        }

        public void OnEnable()
        {
            GameEvent.OnCityHeadbarShowInfoChange += OnCityHeadbarShowInfoChange;
        }
        public void OnDisable()
        {
            GameEvent.OnCityHeadbarShowInfoChange -= OnCityHeadbarShowInfoChange;
        }

        void OnCityHeadbarShowInfoChange()
        {
            if (info != null)
                info.enabled = showIndo;
        }
    }
}
