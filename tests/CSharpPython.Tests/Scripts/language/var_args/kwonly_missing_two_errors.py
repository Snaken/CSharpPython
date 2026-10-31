def f(*, a, b):
    return a
try:
    f()
except TypeError as e:
    print(e)
