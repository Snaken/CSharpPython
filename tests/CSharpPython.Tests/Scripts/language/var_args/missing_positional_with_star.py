def f(a, b, *r):
    return a
try:
    f(1)
except TypeError as e:
    print(e)
