def f(*, a):
    return a
try:
    f(1)
except TypeError as e:
    print(e)
