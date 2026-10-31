def f():
    i = 0
    def bump():
        nonlocal i
        i += 1
    while i < 5:
        bump()
    return i
print(f())
