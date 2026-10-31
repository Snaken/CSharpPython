def f():
    n = 0
    def bump():
        nonlocal n
        n += 1
        return n
    return [bump() for _ in range(3)], n
print(f())
