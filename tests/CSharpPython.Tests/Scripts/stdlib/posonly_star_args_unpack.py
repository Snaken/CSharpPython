def f(a, b, /, c):
    return a + b + c
args = (1, 2)
print(f(*args, 3), f(*args, c=3), f(*[1, 2, 3]))
kw = {'c': 3}
print(f(1, 2, **kw))
try:
    f(**{'a': 1, 'b': 2, 'c': 3})
except TypeError:
    print('TypeError')
