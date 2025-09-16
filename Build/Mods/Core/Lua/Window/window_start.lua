---@class Window_Start
---@param self Window_Start
Window_Start = Class(Window)

function Window_Start:Awake()

end

function Window_Start:OnButton_newGame()
    --Core.OnLoadGame()
    print("OnButton_newGame")
    Sango.Game.Game.Instance:StartNewGame();
end

function Window_Start:OnButton_mapEditor()
    --Core.OnLoadGame()
    print("OnButton_mapEditor")
    Sango.Game.Game.Instance:EnterMapEditor();
end

function Window_Start:OnButton_test()
    --Core.OnLoadGame()
    print("OnButton_test")
    local path = Sango.Path.FindFile("Scenario/Scenario.json");
    local scenario = Sango.Game.Scenario(path);
    --scenario.CommonData = Sango.Game.GameData.Instance:LoadCommonData();
    --//EnterMapEdior();
    Sango.Game.Scenario.StartScenario(scenario);
    --//scenario.Save(Path.ContentRootPath + "/Save/Scenario.xml");
end