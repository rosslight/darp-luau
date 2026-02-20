--!strict

local test01 = require("./test01")
local test02 = require("./test02")

local nSum: number = test01.add(1, 2)
local nDifference: number = test02.subtract(7, 2)

_G.result = nSum * nDifference

return 
    nSum, 
    nDifference, 
    { 
        sum = nSum, 
        difference = nDifference, 
    }
