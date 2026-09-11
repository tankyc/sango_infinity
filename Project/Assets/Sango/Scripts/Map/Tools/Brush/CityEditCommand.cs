using Sango.Core;
using Sango.Render;
using Sango.Tools.UndoRedo;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Sango.Tools
{
    public class CityEditCommand : IUndoableCommand
    {
        private City city;
        private string propertyName;
        private object oldValue;
        private object newValue;
        private string actionName;

        public CityEditCommand(City city, string propertyName, object oldValue, object newValue, string actionName)
        {
            this.city = city;
            this.propertyName = propertyName;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.actionName = actionName;
        }

        public string Description
        {
            get { return actionName; }
        }

        public void Execute()
        {
            SetPropertyValue(newValue);
        }

        public void Undo()
        {
            SetPropertyValue(oldValue);
        }

        public void Redo()
        {
            Execute();
        }

        public void Destroy()
        {
        }

        private void SetPropertyValue(object value)
        {
            // 相邻城市列表特殊处理:使用List<City>快照重建列表
            if (propertyName.Equals("NeighborList", System.StringComparison.OrdinalIgnoreCase))
            {
                ApplyNeighborList(value as List<City>);
                return;
            }

            switch (propertyName)
            {
                case "food":
                    city.food = (int)value;
                    break;
                case "gold":
                    city.gold = (int)value;
                    break;
                case "population":
                    city.population = (int)value;
                    break;
                case "trooppopulation":
                    city.troopPopulation = (int)value;
                    break;
                case "workingappointtype":
                    city.workingAppointType = (int)value;
                    break;
                case "commerce":
                    city.commerce = (int)value;
                    break;
                case "agriculture":
                    city.agriculture = (int)value;
                    break;
                case "popularsupport":
                    city.popularSupport = System.Convert.ToByte(value);
                    break;
                case "security":
                    city.security = (int)value;
                    break;
                case "energy":
                    city.energy = (int)value;
                    break;
                case "morale":
                    city.morale = (int)value;
                    break;
                case "maxmorale":
                    city.MaxMorale = (int)value;
                    break;
                case "hasbusiness":
                    city.hasBusiness = System.Convert.ToByte(value);
                    break;
                case "troops":
                    city.troops = (int)value;
                    break;
                case "woundedtroops":
                    city.woundedTroops = (int)value;
                    break;
                case "troopslimit":
                    city.troopsLimit = (int)value;
                    break;
                case "storelimit":
                    city.storeLimit = (int)value;
                    break;
                case "goldlimit":
                    city.goldLimit = (int)value;
                    break;
                case "foodlimit":
                    city.foodLimit = (int)value;
                    break;
                case "basegainingold":
                    city.baseGainGold = (int)value;
                    break;
                case "basegainfood":
                    city.baseGainFood = (int)value;
                    break;
                case "commercelimit":
                    city.commerceLimit = (int)value;
                    break;
                case "agriculturelimit":
                    city.agricultureLimit = (int)value;
                    break;
                case "durabilitylimit":
                    city.durabilityLimit = (int)value;
                    break;
                case "durability":
                    city.durability = (int)value;
                    break;
                case "x":
                    city.x = (int)value;
                    break;
                case "y":
                    city.y = (int)value;
                    break;
                case "rot":
                    city.rot = (float)value;
                    break;
                case "heightoffset":
                    city.heightOffset = (float)value;
                    break;
                case "totalgainfood":
                    city.totalGainFood = (int)value;
                    break;
                case "totalgainingold":
                    city.totalGainGold = (int)value;
                    break;
                case "extragainfoodfactor":
                    city.extraGainFoodFactor = (float)value;
                    break;
                case "extragainingoldfactor":
                    city.extraGainGoldFactor = (float)value;
                    break;
                case "extrapolulationfactor":
                    city.extraPopulationFactor = (float)value;
                    break;
                case "population_increase_factor":
                    city.population_increase_factor = (float)value;
                    break;
                case "personhole":
                    city.PersonHole = (int)value;
                    break;
                default:
                    // 未在switch中明确处理的JsonProperty字段,通过反射设置
                    SetPropertyByReflection(propertyName, value);
                    break;
            }
        }

        /// <summary>
        /// 通过反射设置城市字段或属性值,用于支持新增的JsonProperty字段
        /// </summary>
        /// <param name="name">字段或属性名</param>
        /// <param name="value">目标值</param>
        private void SetPropertyByReflection(string name, object value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // 先按精确名称查找字段
            FieldInfo field = typeof(City).GetField(name, flags);
            if (field != null)
            {
                field.SetValue(city, value);
                return;
            }

            // 再按精确名称查找属性
            PropertyInfo property = typeof(City).GetProperty(name, flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(city, value, null);
                return;
            }

            // 兼容旧调用传入的小写字段名,进行大小写不敏感查找
            field = typeof(City).GetField(name, BindingFlags.IgnoreCase | flags);
            if (field != null)
            {
                field.SetValue(city, value);
                return;
            }
            property = typeof(City).GetProperty(name, BindingFlags.IgnoreCase | flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(city, value, null);
                return;
            }

            Sango.Log.Warning($"城市属性命令未找到可设置的属性: {name}");
        }

        /// <summary>
        /// 应用相邻城市列表(通过List<City>快照重建)
        /// </summary>
        /// <param name="list">城市列表快照</param>
        private void ApplyNeighborList(List<City> list)
        {
            city.NeighborList.Clear();
            if (list == null)
            {
                return;
            }
            foreach (City neighbor in list)
            {
                if (neighbor != null && neighbor != city)
                {
                    city.NeighborList.Add(neighbor);
                }
            }
        }
    }
}