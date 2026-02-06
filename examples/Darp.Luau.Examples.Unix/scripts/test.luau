--!strict

function add(a: number, b: number): ()
    _G.log(string.format("add: a=%d, b=%d", a, b))
    return a + b
end

function subtract(a: number, b: number): ()
    _G.log(string.format("subtract: a=%d, b=%d", a, b))
    return a - b
end

return
{
    add = add,
    subtract = subtract,
}
