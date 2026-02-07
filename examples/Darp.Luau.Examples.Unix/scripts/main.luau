--!strict

local function fromHexString(str: string): buffer
    local nLen: number = string.len(str)
    if nLen % 2 ~= 0 then
        error("Odd buffer length")
    end

    local nLenBuf: number = nLen / 2
    local bufResult: buffer = buffer.create(nLenBuf)
    local iStr: number = 1
    for iBuf = 0, nLenBuf - 1, 1 do
        local strByte: string = string.sub(str, iStr, iStr + 1)
        local nByte: number? = tonumber(strByte, 16)
        if nByte == nil then
            error(string.format("'%s' not convertible", strByte))
        end

        buffer.writeu8(bufResult, iBuf, nByte)
        iStr += 2
    end

    return bufResult
end


local function parse(strData: string): boolean
    local data: buffer = fromHexString(strData)
    _G.TheParseResult = _G.apple.parse(data)
    return true
end


_G.parse = parse
