def f():
    for i in range(2):
        pass
    del_ = j
    j = 1
f()

# ---
def f(a):
    print(b)
    b = a
f(1)

# ---
def f():
    if False:
        x = 1
    return x
f()

# ---
def f():
    try:
        y = 1 / 0
    except ZeroDivisionError:
        pass
    return y
f()

# ---
def f():
    for i in []:
        z = i
    return z
f()

# ---
def f(a):
    pass
f()

# ---
def f(a):
    pass
f(1, 2)

# ---
def f(a, b=1):
    pass
f()

# ---
def f(a, b=1):
    pass
f(1, 2, 3)

# ---
def f(a):
    pass
f(b=1)

# ---
def f(a):
    pass
f(1, a=1)

# ---
def f(a, b):
    pass
f(1, a=1, b=2)

# ---
def f(a, b):
    pass
f(b=1)

# ---
def f(a, b, c):
    pass
f(c=1)

# ---
def f(a, b):
    pass
f(1, c=2)

# ---
def f():
    pass
f(a=1)

# ---
def f():
    return f()
f()

# ---
def f(n):
    return g(n)
def g(n):
    return f(n)
f(1)

# ---
def f():
    f()
f()

# ---
raise ValueError('x')
