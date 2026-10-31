def f():
    n = 2
    def mul(v):
        return v * n
    return mul(1), mul(2), mul(3)
print(f())
