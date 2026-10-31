def f(*a):
    return a
try:
    f(x=1)
except TypeError as e:
    print(e)
