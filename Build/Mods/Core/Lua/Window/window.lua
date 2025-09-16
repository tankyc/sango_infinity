--- 这里定义了游戏路径如何获取
local Sango = Sango;
---@class Window
local Window = {}
---------------------------------------------------------------------------
---
function Window:Show()
    if self.Behaviour ~= nil then
        self.Behaviour:Show();
    end
end

function Window:Hide()
    if self.Behaviour ~= nil then
        self.Behaviour:Hide();
    end
end

local TypeOfUIText = typeof(UnityEngine.UI.Text);
local TypeOfUIButton = typeof(UnityEngine.UI.Button);
local TypeOfUIImage = typeof(UnityEngine.UI.Image);
local TypeOfUIRawImage= typeof(UnityEngine.UI.RawImage);

Window.TypeOfUIText = TypeOfUIText
Window.TypeOfUIButton = TypeOfUIButton
Window.TypeOfUIImage = TypeOfUIImage
Window.TypeOfUIRawImage = TypeOfUIRawImage


function Window:GetText(name)
    if self.Behaviour == nil then
        return ;
    end
    return self.Behaviour:GetComponent(name, TypeOfUIText)
end

function Window:GetImage(name)
    if self.Behaviour == nil then
        return ;
    end
    return self.Behaviour:GetComponent(name, TypeOfUIImage)
end

function Window:GetRawImage(name)
    if self.Behaviour == nil then
        return ;
    end
    return self.Behaviour:GetComponent(name, TypeOfUIRawImage)
end

function Window:GetTransform(name)
    if self.Behaviour == nil then
        return ;
    end
    return self.Behaviour:GetTransform(name)
end

function Window:GetObject(name)
    if self.Behaviour == nil then
        return ;
    end
    return self.Behaviour:GetObject(name)
end


return Window;