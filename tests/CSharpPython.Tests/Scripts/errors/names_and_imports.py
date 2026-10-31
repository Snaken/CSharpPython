1 + undefined

# ---
[undefined]

# ---
{undefined: 1}

# ---
x = undefined

# ---
for i in undefined:
    pass

# ---
undefined[0]

# ---
undefined[0] = 1

# ---
undefined += 1

# ---
undefined.x = 1

# ---
print(undefined_name)

# ---
# expected: ImportError (CPython raises ModuleNotFoundError)
import nosuchmodule

# ---
from math import nope

# ---
# expected: ImportError (CPython raises ModuleNotFoundError)
from nosuch import x

# ---
import math
math.nope

# ---
import math
math.nope()

# ---
import math
math.sqrt()
