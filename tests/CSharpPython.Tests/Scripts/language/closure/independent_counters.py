def make():
    n = 0
    def inc():
        nonlocal n
        n += 1
        return n
    return inc
a = make()
b = make()
a()
a()
print(a(), b())
