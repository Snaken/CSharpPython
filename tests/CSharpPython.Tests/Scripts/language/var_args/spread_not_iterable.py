def f(*a):
    return a
try:
    f(*5)
except TypeError:
    print('TypeError')
