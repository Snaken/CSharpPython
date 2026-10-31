def f():
    n = 1
    def g():
        return n
    n = 2
    r1 = g()
    n = 3
    return r1, g()
print(f())
