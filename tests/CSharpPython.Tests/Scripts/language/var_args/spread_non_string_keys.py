def f(**k):
    return k
try:
    f(**{1: 2})
except TypeError as e:
    print(e)
