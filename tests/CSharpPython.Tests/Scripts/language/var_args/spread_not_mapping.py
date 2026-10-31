def f(**k):
    return k
try:
    f(**[1])
except TypeError:
    print('TypeError')
