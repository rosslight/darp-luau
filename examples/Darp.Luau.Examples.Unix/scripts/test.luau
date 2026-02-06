--!strict

type ParseResult = { SubTypeName: string, [string] : string | number | boolean }


function parse(strData: string) : ParseResult?
    local result: ParseResult = { SubTypeName = "Beacon" }
    result["Data"] = strData
    result["_String_"] = "abc"
    result["_Boolean_"] = true
    result["_Number_"] = 444
    return result
    -- return nil
end


print(_G.Data)
_G.Result = parse(_G.Data)
print(_G.Result)


return
{
    parse = parse,
}

