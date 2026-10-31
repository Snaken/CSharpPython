def f(x):
    def g():
        return x * 2
    return g
print(f(21)())
