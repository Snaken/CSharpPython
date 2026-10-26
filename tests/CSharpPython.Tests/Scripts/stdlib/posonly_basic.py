def f(a, b, /, c, d=4):
    return (a, b, c, d)
print(f(1, 2, 3), f(1, 2, c=3), f(1, 2, 3, 5), f(1, 2, d=9, c=3))
def t(fn):
    try:
        print(fn())
    except TypeError as e:
        print('TypeError')
t(lambda: f(1, b=2, c=3))
t(lambda: f(a=1, b=2, c=3))
t(lambda: f(1, 2))
t(lambda: f(1))
t(lambda: f(1, 2, 3, 4, 5))
