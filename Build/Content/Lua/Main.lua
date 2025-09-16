-- Tolua主入口函数。从这里开始lua逻辑
----- 加载mod 以后考虑异步加载

require("class")


--local t = {}
--
--t.__index = t;
--function t:ddd()
--    if t.index == nil then
--        t.index = 1;
--    end
--    t.index = t.index + 1;
--end
--
--local tt = {}
--for i = 1, 10000 do
--    local _t = {}
--    setmetatable(_t, t);
--    table.insert(tt, t);
--end
--
--local time = os.clock();
--print(time);
--local func = t.ddd;
--for i = 1, 10000000 do
--    func(tt[1]);
--    --tt[1]:ddd();
--end
--print(os.clock() - time);
--time = os.clock();


--MailItem = fgui.extension_class(GButton)
--fgui.register_extension("ui://VirtualList/mailItem", MailItem)
--
--function MailItem:ctor()
--    --当组件构建完成时此方法被调用
--    self._timeText = self:GetChild("timeText").asTextField;
--    self._readController = self:GetController("IsRead");
--    self._fetchController = self:GetController("c1");
--end
--
--function MailItem:setTime(value)
--    self._timeText.text = value;
--end
--
--function MailItem:setRead(value)
--    self._readController.selectedIndex = value and 1 or 0;
--end
--
--function MailItem:setFetched(value)
--    self._fetchController.selectedIndex = value and 1 or 0;
--end


--local get = tolua.initget(MyButton)
--local set = tolua.initset(MyButton)
--get.myProp = function(self)
--    return self._myProp
--end
--
--set.myProp = function(self, value)
--    self._myProp = value
--    self:GetChild('n1').text = value
--end
--
--local myButton = someComponent:GetChild("myButton") --这个myButton的资源是“我的按钮”
--myButton:Test()
--myButton.myProp = 'hello'
--
--local myButton2 = UIPackage.CreateObject("包名","我的按钮")
--myButton2:Test()
--myButton2.myProp = 'world'
function Main()
    --require( "Sango/sango").Start();

    --ModManager.Init();
    --ModManager.AsyncLoadMods(function()
    --    StateManager.Init("GameStart");
    --end);
    --
    --UIPackage.AddPackage("UI/VirtualList");
    --local contentPane = UIPackage.CreateObject("VirtualList", "Main");
    --GRoot.inst:AddChild(contentPane);
    --_list = contentPane:GetChild("mailList").asList;
    --
    --contentPane:GetChild("n6").onClick:Add(function() _list:AddSelection(500, true) end);
    --contentPane:GetChild("n7").onClick:Add(function() _list.scrollPane:ScrollTop() end);
    --contentPane:GetChild("n8").onClick:Add(function()  _list.scrollPane:ScrollBottom() end);
    --
    --_list:SetVirtual();
    --
    --_list.itemRenderer = function(index, obj)
    --    obj.title = index .. " Mail title here";
    --    obj:setFetched(index % 3 == 0);
    --    obj:setRead(index % 2 == 0);
    --    obj:setTime("5 Nov 2015 16:24:33");
    --end;
    --_list.numItems = 1000;
end



