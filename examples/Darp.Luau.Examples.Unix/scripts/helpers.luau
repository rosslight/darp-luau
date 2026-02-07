--!strict

local function toHexString(buf: buffer, strDelim: string?): string
    local str: string = ""
    for i = 0, buffer.len(buf) - 1, 1 do
        if strDelim ~= nil and i > 0 then
            str ..= strDelim
        end

        local nValue: number = buffer.readu8(buf, i)
        str ..= string.format("%02X", nValue)
   end
   return str
end


local function fromHexString(str: string): buffer
    local nLen: number = string.len(str)
    if nLen % 2 ~= 0 then
        error("Bufferlänge ist ungerade")
    end

    local nLenBuf: number = nLen / 2
    local bufResult: buffer = buffer.create(nLenBuf)
    local iStr: number = 1
    for iBuf = 0, nLenBuf - 1, 1 do
        local strByte: string = string.sub(str, iStr, iStr + 1)
        local nByte: number? = tonumber(strByte, 16)
        if nByte == nil then
            error(string.format("'%s' konnte nicht konvertiert werden", strByte))
        end

        buffer.writeu8(bufResult, iBuf, nByte)
        iStr += 2
    end

    return bufResult
end


_G.helpers = 
{
    toHexString = toHexString,
    fromHexString = fromHexString,
}
