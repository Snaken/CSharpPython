def f(**k):
    return k
try:
    f(**{'a': 1}, **{'a': 2})
except TypeError as e:
    print('repeated')
