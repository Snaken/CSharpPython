def f(*a):
    t = 0
    for x in a:
        t += x
    return t
print(f(1, 2, 3, 4))
