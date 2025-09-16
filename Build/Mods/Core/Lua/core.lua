--- 加载核心代码

--- 当前模组代码所在位置
---@class Core
Core = { }


--require "Core/event";
--require "Core/object"
--require "Core/common";
--require "Core/controller"
--require "Core/scenario"
--require "Core/command"
--
-----@type config
--Sango.Config = Sango.Data.Get("config");
--
-----窗口矩形
--ScreenRect = UnityEngine.Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height)
--
function Core.Init()
    print("Core.Init")
end
--
-----开始剧本
-----@param scenarioInfo ScenarioInfo
--function Core.StartScenario(scenarioInfo)
--    StartVirtualThread(function(thread)
--        Scenario.Start(scenarioInfo, thread);
--    end)
--end
