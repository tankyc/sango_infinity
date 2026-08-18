using UnityEngine;
using Sango.Core;
using Sango.Render;
using Sango.Tools.UndoRedo;
using System.Collections.Generic;

namespace Sango.Tools
{
    public class CityPropertyEditorWindow : EditorWindow
    {
        private MapEditor editor;
        private City selectedCity;
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// 武将归属列表滚动位置
        /// </summary>
        private Vector2 personAssignScroll = Vector2.zero;

        public void Initialize(MapEditor editor, City city)
        {
            this.editor = editor;
            this.selectedCity = city;
            this.visible = true;

            float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 300);
            windowRect = new UnityEngine.Rect(
                Screen.width / 2 - 200,
                windowY,
                400,
                600
            );
        }

        public void SetCity(City city)
        {
            if (selectedCity != city)
            {
                if (selectedCity != null && selectedCity.Render != null)
                {
                    selectedCity.Render.SetFlash(false);
                }
                selectedCity = city;
                if (selectedCity != null && selectedCity.Render != null)
                {
                    selectedCity.Render.SetFlash(true);
                }
            }
        }

        private void OnDrawWindow(int winId)
        {
            if (selectedCity == null)
            {
                GUILayout.Label("未选中城市");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"城市属性编辑: {selectedCity.Name}", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16, alignment = TextAnchor.MiddleCenter });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("x", GUILayout.Width(30)))
            {
                CloseWindow();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("", GUI.skin.horizontalSlider);
            GUILayout.Space(10);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(windowRect.width - 20), GUILayout.Height(windowRect.height - 160));

            DrawBasicProperties();
            DrawDevelopmentProperties();
            DrawMilitaryProperties();
            DrawLimitProperties();
            DrawIncomeProperties();
            DrawGrowthFactors();
            DrawOtherProperties();

            DrawOwnershipProperties();

            DrawPersonAssignment();

            GUILayout.EndScrollView();
        }

        private void CloseWindow()
        {
            if (selectedCity != null && selectedCity.Render != null)
            {
                selectedCity.Render.SetFlash(false);
            }
            visible = false;
            Destroy(gameObject);
        }

        private void DrawBasicProperties()
        {
            GUILayout.Label("基础属性");
            GUILayout.Space(5);
            selectedCity.Name = EditCityProperty("名字", selectedCity.Name);

            EditCityProperty("粮食", ref selectedCity.food, 0, 999999);
            EditCityProperty("金钱", ref selectedCity.gold, 0, 999999);
            EditCityProperty("人口", ref selectedCity.population, 0, 999999);
            EditCityProperty("兵役人口", ref selectedCity.troopPopulation, 0, 999999);
            EditCityProperty("工作委任类型", ref selectedCity.workingAppointType, 0, 99);
            GUILayout.Space(10);
        }

        private void DrawDevelopmentProperties()
        {
            GUILayout.Label("发展属性");
            GUILayout.Space(5);
            EditCityProperty("商业", ref selectedCity.commerce, 0, 999);
            EditCityProperty("农业", ref selectedCity.agriculture, 0, 999);
            EditCityProperty("民心", ref selectedCity.popularSupport, 0, 100);
            EditCityProperty("治安", ref selectedCity.security, 0, 100);
            GUILayout.Space(10);
        }

        private void DrawMilitaryProperties()
        {
            GUILayout.Label("军事属性");
            GUILayout.Space(5);
            EditCityProperty("战意", ref selectedCity.energy, 0, 100);
            EditCityProperty("士气", ref selectedCity.morale, 0, selectedCity.MaxMorale);
            int maxMorale = selectedCity.MaxMorale;
            EditCityProperty("最大士气", ref maxMorale, 0, 100);
            if (maxMorale != selectedCity.MaxMorale)
            {
                CreateEditCommand("MaxMorale", selectedCity.MaxMorale, maxMorale, $"修改城市最大士气: {selectedCity.MaxMorale} -> {maxMorale}");
                selectedCity.MaxMorale = maxMorale;
            }
            EditCityProperty("兵力", ref selectedCity.troops, 0, selectedCity.TroopsLimit);
            EditCityProperty("伤兵", ref selectedCity.woundedTroops, 0, 999999);
            GUILayout.Space(10);
        }

        private void DrawLimitProperties()
        {
            GUILayout.Label("限制属性");
            GUILayout.Space(5);
            EditCityProperty("可容纳兵力", ref selectedCity.troopsLimit, 0, 999999);
            EditCityProperty("仓库大小", ref selectedCity.storeLimit, 0, 999999);
            EditCityProperty("金库大小", ref selectedCity.goldLimit, 0, 999999);
            EditCityProperty("粮仓大小", ref selectedCity.foodLimit, 0, 999999);
            GUILayout.Space(10);
        }

        private void DrawIncomeProperties()
        {
            GUILayout.Label("收入属性");
            GUILayout.Space(5);
            EditCityProperty("基础金钱收入", ref selectedCity.baseGainGold, 0, 999999);
            EditCityProperty("基础粮食收入", ref selectedCity.baseGainFood, 0, 999999);
            EditCityProperty("总粮食收入", ref selectedCity.totalGainFood, 0, 999999);
            EditCityProperty("总金钱收入", ref selectedCity.totalGainGold, 0, 999999);
            GUILayout.Space(10);
        }

        private void DrawGrowthFactors()
        {
            GUILayout.Label("增长因子");
            GUILayout.Space(5);
            EditCityProperty("粮食收入倍率", ref selectedCity.extraGainFoodFactor, 0f, 10f);
            EditCityProperty("金钱收入倍率", ref selectedCity.extraGainGoldFactor, 0f, 10f);
            EditCityProperty("人口增长倍率", ref selectedCity.extraPopulationFactor, 0f, 10f);
            EditCityProperty("人口增长因子", ref selectedCity.population_increase_factor, 0f, 1f);
            GUILayout.Space(10);
        }

        private void DrawOtherProperties()
        {
            GUILayout.Label("其他属性");
            GUILayout.Space(5);
            EditCityProperty("耐久度", ref selectedCity.durability, 0, selectedCity.DurabilityLimit);
            EditCityProperty("最大耐久", ref selectedCity.durabilityLimit, 0, 999999);
            EditCityProperty("商人比例", ref selectedCity.hasBusiness, 0, 100);
            int personHole = selectedCity.PersonHole;
            EditCityProperty("人员容纳上限", ref personHole, 0, 999);
            if (personHole != selectedCity.PersonHole)
            {
                CreateEditCommand("PersonHole", selectedCity.PersonHole, personHole, $"修改城市人员容纳上限: {selectedCity.PersonHole} -> {personHole}");
                selectedCity.PersonHole = personHole;
            }
            EditCityProperty("X坐标", ref selectedCity.x, 0, 9999);
            EditCityProperty("Y坐标", ref selectedCity.y, 0, 9999);
            EditCityProperty("旋转值", ref selectedCity.rot, 0f, 360f);
            EditCityProperty("高度偏移", ref selectedCity.heightOffset, -100f, 100f);
        }

        private void EditCityProperty(string name, ref int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(100));
            string newValueStr = GUILayout.TextField(value.ToString(), GUILayout.Width(100));
            if (int.TryParse(newValueStr, out int newValue) && newValue != value)
            {
                newValue = Mathf.Clamp(newValue, min, max);
                CreateEditCommand(name.ToLower(), value, newValue, $"修改城市{name}: {value} -> {newValue}");
                value = newValue;
            }
            GUILayout.EndHorizontal();
        }

        private string EditCityProperty(string name, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(100));
            string newValueStr = GUILayout.TextField(value, GUILayout.Width(100));
            if (newValueStr.Equals(value))
            {
                CreateEditCommand(name.ToLower(), value, newValueStr, $"修改城市{name}: {value} -> {newValueStr}");
                value = newValueStr;
            }
            GUILayout.EndHorizontal();
            return value;
        }

        private void EditCityProperty(string name, ref byte value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(100));
            string newValueStr = GUILayout.TextField(value.ToString(), GUILayout.Width(100));
            if (int.TryParse(newValueStr, out int newValue) && newValue != value)
            {
                newValue = Mathf.Clamp(newValue, min, max);
                byte newByteValue = (byte)newValue;
                CreateEditCommand(name.ToLower(), value, newByteValue, $"修改城市{name}: {value} -> {newByteValue}");
                value = newByteValue;
            }
            GUILayout.EndHorizontal();
        }

        private void EditCityProperty(string name, ref float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(100));
            string newValueStr = GUILayout.TextField(value.ToString("F2"), GUILayout.Width(100));
            if (float.TryParse(newValueStr, out float newValue) && newValue != value)
            {
                newValue = Mathf.Clamp(newValue, min, max);
                CreateEditCommand(name.ToLower(), value, newValue, $"修改城市{name}: {value:F2} -> {newValue:F2}");
                value = newValue;
            }
            GUILayout.EndHorizontal();
        }

        private void CreateEditCommand(string propertyName, object oldValue, object newValue, string actionName)
        {
            if (editor != null && editor.undoRedoManager != null)
            {
                CityEditCommand command = new CityEditCommand(editor, selectedCity, propertyName, oldValue, newValue, actionName);
                editor.undoRedoManager.AddCommand(command, true);
            }
        }

        private new void OnGUI()
        {
            if (visible && selectedCity != null)
            {
                windowRect = GUILayout.Window(windowId, windowRect, OnDrawWindow, windowName);
                ConstrainWindowToScreen();
            }
        }


        /// <summary>
        /// 绘制城池归属势力和归属军团
        /// </summary>
        private void DrawOwnershipProperties()
        {
            GUILayout.Label("归属势力");
            GUILayout.Space(5);

            List<Force> forces = new List<Force>();
            List<string> forceNames = new List<string>();
            forces.Add(null);
            forceNames.Add("无");
            editor.scenario.forceSet.ForEach((Force force) =>
            {
                if (force != null)
                {
                    forces.Add(force);
                    forceNames.Add($"{force.Id}:{force.Name}");
                }
            });

            int forceIndex = System.Math.Max(0, forces.IndexOf(selectedCity.mBelongForce));
            int newForceIndex = GUILayout.SelectionGrid(forceIndex, forceNames.ToArray(), 1);
            if (newForceIndex != forceIndex && newForceIndex >= 0 && newForceIndex < forces.Count)
            {
                Force newForce = forces[newForceIndex];
                selectedCity.mBelongForce = newForce;
                foreach (Person person in editor.scenario.personSet)
                {
                    if (person != null && person.mBelongCity == selectedCity)
                    {
                        person.mBelongForce = newForce;
                    }
                }
            }
            GUILayout.Space(10);

            GUILayout.Label("归属军团");
            GUILayout.Space(5);

            List<Corps> corpsList = new List<Corps>();
            List<string> corpsNames = new List<string>();
            corpsList.Add(null);
            corpsNames.Add("无");
            Force cityForce = selectedCity.mBelongForce;
            editor.scenario.corpsSet.ForEach((Corps corps) =>
            {
                if (corps != null && (cityForce == null || corps.mBelongForce == cityForce))
                {
                    corpsList.Add(corps);
                    corpsNames.Add($"{corps.Id}:{corps.Name}");
                }
            });

            int corpsIndex = System.Math.Max(0, corpsList.IndexOf(selectedCity.mBelongCorps));
            int newCorpsIndex = GUILayout.SelectionGrid(corpsIndex, corpsNames.ToArray(), 1);
            if (newCorpsIndex != corpsIndex && newCorpsIndex >= 0 && newCorpsIndex < corpsList.Count)
            {
                Corps newCorps = corpsList[newCorpsIndex];
                selectedCity.mBelongCorps = newCorps;
                foreach (Person person in editor.scenario.personSet)
                {
                    if (person != null && person.mBelongCity == selectedCity)
                    {
                        person.mBelongCorps = newCorps;
                    }
                }
            }
            GUILayout.Space(10);
        }

        /// <summary>
        /// 绘制城池武将归属列表
        /// </summary>
        private void DrawPersonAssignment()
        {
            GUILayout.Label("城池武将归属");
            GUILayout.Space(5);

            personAssignScroll = GUILayout.BeginScrollView(personAssignScroll, GUILayout.Height(200));
            foreach (Person person in editor.scenario.personSet)
            {
                if (person == null)
                {
                    continue;
                }
                bool assigned = person.mBelongCity == selectedCity;
                GUILayout.BeginHorizontal();
                bool newAssigned = GUILayout.Toggle(assigned, "", GUILayout.Width(20));
                GUILayout.Label($"{person.Id}:{person.Name}");
                GUILayout.EndHorizontal();
                if (newAssigned != assigned)
                {
                    if (newAssigned)
                    {
                        person.mBelongCity = selectedCity;
                        person.mCurrentCity = selectedCity;
                        person.mBelongForce = selectedCity.mBelongForce;
                        person.mBelongCorps = selectedCity.mBelongCorps;
                    }
                    else
                    {
                        person.mBelongCity = null;
                        person.mCurrentCity = null;
                        person.mBelongForce = null;
                        person.mBelongCorps = null;
                    }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Space(10);
        }
    }
}