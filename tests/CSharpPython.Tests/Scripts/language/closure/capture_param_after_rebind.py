def f(x):
    def g():
        return x
    x = 5
    return g()
print(f(1))
