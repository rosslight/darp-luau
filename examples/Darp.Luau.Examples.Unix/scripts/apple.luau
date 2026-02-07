--!strict

type ParseResult = { SubTypeName: string, [string] : string | number | boolean }


local function getPayload(data: buffer, nMinLength: number): buffer?
    local nLenBuf: number = buffer.len(data)
    if nLenBuf < 2 then return nil end

    local nLength: number = buffer.readu8(data, 1)
    if nLength < nMinLength then return nil end
    if nLenBuf - 2 < nLength then return nil end

    local payload: buffer = buffer.create(nLenBuf - 2)
    buffer.copy(payload, 0, data, 2)
    return payload
end


local function getValue(payload: buffer, nOffset: number, nCount: number): buffer
    local value: buffer = buffer.create(nCount)
    buffer.copy(value, 0, payload, nOffset, nCount)
    return value
end


local function parseBeacon(data: buffer): ParseResult?
    local payload: buffer? = getPayload(data, 0x15)
    if payload == nil then return nil end

    local result: ParseResult = { SubTypeName = "Beacon" }
    result["ProximityUuid"] = _G.helpers.toHexString(getValue(payload, 0, 16))
    result["Major"] = _G.helpers.toHexString(getValue(payload, 16, 2))
    result["Minor"] = _G.helpers.toHexString(getValue(payload, 18, 2))
    result["MeasuredPower"] = _G.helpers.toHexString(getValue(payload, 20, 1))
    return result
end


local function parseNearbyInfo(data: buffer): ParseResult?
    local payload: buffer? = getPayload(data, 0x05)
    if payload == nil then return nil end

    local result: ParseResult = { SubTypeName = "NearbyInfo" }
    result["StatusFlags"] = string.format("%02X", buffer.readbits(payload, 0, 4)) -- Low order nibble
    result["ActionCode"] = string.format("%02X", buffer.readbits(payload, 4, 4)) -- High order nibble
    result["DataFlags"] = _G.helpers.toHexString(getValue(payload, 1, 1))
    result["AuthenticationTag"] = _G.helpers.toHexString(getValue(payload, 2, 3))

    if buffer.len(payload) > 5 then
        result["PostAuthenticationTag"] = _G.helpers.toHexString(getValue(payload, 5, 1))
    end

    return result
end


local function parse(data: buffer): ParseResult?
    if buffer.len(data) > 0 then
        local nType: number = buffer.readu8(data, 0)
        if nType == 0x02 then return parseBeacon(data) end
        if nType == 0x10 then return parseNearbyInfo(data) end
    end
    
    return nil
end


_G.apple =
{
    parse = parse,
}