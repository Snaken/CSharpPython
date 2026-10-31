def f(n):
    def g(a, b=0):
        return a * n + b
    return g(b=1, a=2)
print(f(10))
