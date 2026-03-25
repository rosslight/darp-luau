--!strict

local function parse(data: buffer): boolean
    _G.TheParseResult = _G.apple.parse(data)
    return true
end


_G.parse = parse
