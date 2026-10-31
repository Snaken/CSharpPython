def f(*, a):
    return a
try:
    f()
except TypeError as e:
    print(e)
