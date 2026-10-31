def make():
    n = 0
    def inc():
        nonlocal n
        n += 1
        return n
    return inc
c = make()
c()
c()
print(c())
