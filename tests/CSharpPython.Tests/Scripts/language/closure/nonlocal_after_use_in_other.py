def f():
    a = 1
    def g():
        nonlocal a
        a = a + 1
        return a
    return g(), g(), a
print(f())
