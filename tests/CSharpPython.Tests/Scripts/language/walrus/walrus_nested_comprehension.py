res = [[(m := a * b) for b in range(3)] for a in range(3)]
print(res, m)
def f():
    return [[(m := a + b) for b in range(2)] for a in range(2)], m
print(f())
