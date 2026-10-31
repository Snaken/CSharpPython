def f(a, b=2):
    def g():
        return a + b
    return g()
print(f(1), f(1, 5))
