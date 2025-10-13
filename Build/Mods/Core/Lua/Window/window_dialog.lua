local Window = require "Window/window"
---@class Window_Dialog
---@param self Window_Dialog
Window_Dialog = Class(Window)


function Window_Dialog:Awake()
    self.ContentText = self:GetText("contentBg/content");
end

function Window_Dialog:InitEvent(event, content)
    self.mEvent = event;
    self.ContentText.text = content;
    local co = nil;
    co = coroutine.start(function()
        coroutine.wait(1);
        self.mEvent.IsDone = true;
        self.mEvent = nil;
        coroutine.stop(co);
    end)
end

function Window_Dialog:OnButton_ok()
    self.mEvent.IsDone = true;
    self.mEvent = nil;
end
