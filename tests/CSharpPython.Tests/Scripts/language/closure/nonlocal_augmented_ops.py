def f():
    t = 10
    def g():
        nonlocal t
        t -= 3
        t *= 2
    g()
    return t
print(f())
