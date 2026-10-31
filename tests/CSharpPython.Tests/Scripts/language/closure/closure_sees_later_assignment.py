def f():
    def g():
        return y
    y = 7
    return g()
print(f())
