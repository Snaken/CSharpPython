'abc'.foo()

# ---
[].foo()

# ---
{}.foo()

# ---
().foo()

# ---
(1).foo

# ---
'a'.upper(1)

# ---
'a'.split(1)

# ---
'a'.split('')

# ---
x = 1
x.y

# ---
x = 1
x.y = 2

# ---
x = None
x.y

# ---
x = []
x.y = 1

# ---
x = 'a'
x.y

# ---
x = (1,)
x.y = 1

# ---
def f():
    pass
f.x

# ---
import math
math.sqrt.x

# ---
(1).x

# ---
[].x

# ---
{}.x

# ---
().x

# ---
range(3).x

# ---
len.x

# ---
True.x

# ---
1.5.x

# ---
None.x = 1

# ---
def f():
    return x
    x = 1
f()

# ---
def f():
    x += 1
f()

# ---
def f():
    print(x)
    x = 1
f()
