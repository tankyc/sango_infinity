local Window = require "Window/window"

---@class Window_Scenario_Select
---@param self Window_Scenario_Select
Window_Scenario_Select = Class(Window)

function Window_Scenario_Select:Awake()
    local scenarioList = Sango.Game.Scenario.all_scenario_list;
    local count = scenarioList.Count;
    local item = self:GetObject("ScenarioItem");

    local listNone = self:GetObject("Scroll View/Viewport/Content");
    self.mCurSelectIndex = 0;
    self.mItemList = {}
    self.mScenarioDescText = self:GetText("desc");
    self.mScenarioInfoText = self:GetText("info");
    self.mScenarioPosterImg = self:GetRawImage("poster");
    for i = 1, count do
        local newItem = UnityEngine.GameObject.Instantiate(item);
        newItem:SetActive(true);
        newItem.transform:SetParent(listNone.transform, false);
        local infoText = Sango.UnityTools.GetComponent(newItem, "item/info", Window.TypeOfUIText)
        local scenario = scenarioList[i - 1];
        local scenarioInfo = scenario.Info
        local str = string.format("%d年%d月%d日  %s", scenarioInfo.year, scenarioInfo.month, scenarioInfo.day, scenarioInfo.name);
        infoText.text = str;
        self.mItemList[i] = { newItem, scenario };
        local btn = Sango.UnityTools.GetComponent(newItem, "item", Window.TypeOfUIButton)
        btn.onClick:AddListener(function()
            self:ShowScenario(i);
        end)
    end
    self:ShowScenario(1);
end

function Window_Scenario_Select:Clear()
    for i = 1, #self.mItemList do
        UnityEngine.GameObject.Destroy(self.mItemList[i][1]);
    end
    self.mItemList = {}
end

function Window_Scenario_Select:ShowScenario(index)
    print("ShowScenario =>" .. index)
    self.mCurSelectIndex = index;
    local scenarioList = Sango.Game.Scenario.all_scenario_list;
    local scenario = scenarioList[index - 1];
    local scenarioInfo = scenario.Info
    local str = string.format("%d年%d月%d日  %s", scenarioInfo.year, scenarioInfo.month, scenarioInfo.day, scenarioInfo.name);
    self.mScenarioInfoText.text = str;
    self.mScenarioDescText.text = scenarioInfo.description;
end

function Window_Scenario_Select:OnButton_enter()
    print("OnButton_enter")
    self:Clear();
    local scenarioList = Sango.Game.Scenario.all_scenario_list;
    local scenario = scenarioList[self.mCurSelectIndex - 1];
    Sango.Window.Instance:ShowWindow("window_loading");
    Sango.Window.Instance:HideWindow("window_scenario_select");
    Sango.Game.Scenario.StartScenario(scenario);
end

function Window_Scenario_Select:OnButton_return()
    print("OnButton_return")
    self:Clear();
    Sango.Window.Instance:ShowWindow("window_start");
    Sango.Window.Instance:HideWindow("window_scenario_select");
end

