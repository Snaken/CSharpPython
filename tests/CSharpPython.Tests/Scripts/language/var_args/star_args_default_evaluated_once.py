n = 0
def bump():
    global n
    n += 1
    return n
def f(*, a=bump()):
    return a
print(f(), f(), n)
