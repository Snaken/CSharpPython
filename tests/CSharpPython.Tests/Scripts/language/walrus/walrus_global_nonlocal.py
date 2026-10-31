def f():
    global g
    if (g := 5) > 1:
        return g
print(f(), g)
def outer():
    c = 0
    def inner():
        nonlocal c
        if (c := c + 1) > 0:
            return c
    inner()
    inner()
    return c
print(outer())
