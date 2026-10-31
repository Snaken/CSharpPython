def f(flag):
    v = 1 if flag else 2
    return (lambda: v)()
print(f(True), f(False))
