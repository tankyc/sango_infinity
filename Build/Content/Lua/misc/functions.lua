local require = require
local string = string
local table = table

int64.zero = int64.new(0, 0)
uint64.zero = uint64.new(0, 0)

function table.remove_value(t, v)
    for i = 1, #t do
        if t[i] == v then
            table.remove(t, i);
            return
        end
    end
end

function table.remove_all(t, compareFunc)
    for i = #t, 1, -1 do
        if compareFunc(t[i]) then
            table.remove(t, i);
        end
    end
end

function table.find_all(t, compareFunc)
    local rs = {};
    for k, v in pairs(t) do
        if compareFunc(v) then
            rs[#rs+1] = v;
        end
    end
    return rs;
end

function table.find(t, compareFunc)
    for k, v in pairs(t) do
        if compareFunc(v) then
            return v, k;
        end
    end
end

function table.contains(t, v)
    for _, _v in pairs(t) do
        if _v == v then
            return true
        end
    end
    return false;
end

function table.copy(t)
    local n = {}
    for k, v in pairs(t) do
        if type(v) == 'table' then
            n[k] = table.copy(v);
        else
            n[k] = v;
        end
    end
    return n;
end

function table.merge(dest, from)
    for k, v in pairs(from) do
        dest[k] = v;
    end
end

function string.split(input, delimiter)
    input = tostring(input)
    delimiter = tostring(delimiter)
    if (delimiter == '') then
        return false
    end
    local pos, arr = 0, {}
    -- for each divider found
    for st, sp in function()
        return string.find(input, delimiter, pos, true)
    end do
        table.insert(arr, string.sub(input, pos, st - 1))
        pos = sp + 1
    end
    table.insert(arr, string.sub(input, pos))
    return arr
end

function string.startWith(s, chars, beginIndex)
    local i = beginIndex or 1;
    local subS = string.sub(s, i, i - 1 + string.len(chars));
    return subS == chars;
end

function string.endWith(s, chars)
    return string.startWith(s, chars, string.len(s) - string.len(chars) + 1);
end


function import(moduleName, currentModuleName)
    local currentModuleNameParts
    local moduleFullName = moduleName
    local offset = 1

    while true do
        if string.byte(moduleName, offset) ~= 46 then
            -- .
            moduleFullName = string.sub(moduleName, offset)
            if currentModuleNameParts and #currentModuleNameParts > 0 then
                moduleFullName = table.concat(currentModuleNameParts, ".") .. "." .. moduleFullName
            end
            break
        end
        offset = offset + 1

        if not currentModuleNameParts then
            if not currentModuleName then
                local n, v = debug.getlocal(3, 1)
                currentModuleName = v
            end

            currentModuleNameParts = string.split(currentModuleName, ".")
        end
        table.remove(currentModuleNameParts, #currentModuleNameParts)
    end

    return require(moduleFullName)
end

--重新require一个lua文件，替代系统文件。
function reimport(name)
    local package = package
    package.loaded[name] = nil
    package.preload[name] = nil
    return require(name)
end

