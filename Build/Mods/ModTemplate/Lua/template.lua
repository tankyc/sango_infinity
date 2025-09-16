

local Event = Sango.Event

---@class CitySystem
---@param self CitySystem
local CitySystem = Class(CommandWindow, function(self)

end)

function CitySystem:Do(building)
    self.Building = building;
    if self.MenuData == nil then
        self.MenuData = MenuItem.MakeData();
        self:GetInitMenu(self.MenuData);
    end
    self.mousePos = Input.mousePosition;
    MenuItem.TryShow(self.MenuData, self.mousePos);
    return self;
end

---获取初始化菜单（菜单数据）
function CitySystem:GetInitMenu(menuData)
    local city = self.Building;
    local force = self.Building:GetForce();
    ---判断是否为玩家控制的势力
    if force:IsPlayer() then
        menuData:Add("都市", 100);    ---Interior
        menuData:Add("军事", 200);    ---Military
        menuData:Add("外交", 300);    ---Diplomacy
        menuData:Add("国政", 400);    ---Politics
        menuData:Add("官吏", 500);    ---Official
        menuData:Add("君主", 600);    ---monarch
        Event.PostEvent(Event.Game_City_OpenMenu, menuData, city);

        --else
        --    table.insert(menuData, { name = "获取情报", callFunc = controller.Add })
        --    table.insert(menuData, { name = "细作", callFunc = controller.Add })
        --    table.insert(menuData, { name = "诈降", callFunc = controller.Add })
    end
end
SangoSystem.CitySystem = CitySystem();

return CitySystem