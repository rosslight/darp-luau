--!strict

type ParseResult = { SubTypeName: string, [string] : string | number | boolean }


local function parse(strData: string): boolean
    local data: buffer = _G.helpers.fromHexString(strData)
    _G.TheParseResult = _G.apple.parse(data)
    return true
end


_G.parse = parse
