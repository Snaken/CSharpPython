def f(a, b=1):
    return a
try:
    f(1, 2, 3)
except TypeError as e:
    print(e)
