
local Window = require "Window/window"
---@class Window_Game
---@param self Window_Game
Window_Game = Class(Window)

function Window_Game:Awake()
    print("Window_Game:Awake()")
    self.ForceNameText = self:GetText("bg/force");
    self.DateText = self:GetText("bg/date");
	Sango.Game.Scenario.Cur.Event.OnForceStart = (function(force, scenario)
        if self.ForceNameText ~= nil then
            self.ForceNameText.text = force.Name;
        end
	end);

    self.PauseObj = self:GetObject("bg/pause");
    self.ResumeObj = self:GetObject("bg/resume");


    Sango.Game.Scenario.Cur.Event.OnDayUpdate = (function(scenario)
        if self.DateText ~= nil then
            local info = scenario.Info;
            self.DateText.text = string.format("%s年%s月 %s日", tostring(info.year), tostring(info.month), tostring(info.day));
        end
    end);
end

function Window_Game:OnButton_pause()
    --Core.OnLoadGame()
    print("OnButton_pause")
    self.PauseObj:SetActive(false);
    self.ResumeObj:SetActive(true);
    Sango.Game.Scenario.Pause();
end

function Window_Game:OnButton_resume()
    --Core.OnLoadGame()
    print("OnButton_resume")
    self.PauseObj:SetActive(true);
    self.ResumeObj:SetActive(false);
    Sango.Game.Scenario.Resume();
end

function Window_Game:OnButton_nextForce()
    --Core.OnLoadGame()
    print("OnButton_resume")
    self.PauseObj:SetActive(false);
    self.ResumeObj:SetActive(true);
    Sango.Game.Scenario.NextForce();
end

function Window_Game:OnButton_nextTurn()
    --Core.OnLoadGame()
    print("OnButton_nextTurn")
    self.PauseObj:SetActive(false);
    self.ResumeObj:SetActive(true);
    Sango.Game.Scenario.NextTurn();
end

--function Window_Game:OnButton_nextTurn()
--    --Core.OnLoadGame()
--    print("OnButton_nextTurn")
--    Sango.Game.Game.DebugAI();
--end

function Window_Game:update()
    --Core.OnLoadGame()
    print("OnButton_pause")
end