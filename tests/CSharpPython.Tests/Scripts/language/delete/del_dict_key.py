d = {'a': 1, 'b': 2, 'c': 3}
del d['a']
print(d, len(d))
try:
    del d['zz']
except KeyError as e:
    print('KeyError', e)
try:
    del d[[1]]
except TypeError as e:
    print('TypeError', type(e).__name__)
del d['b'], d['c']
print(d)
d[1] = 'x'
del d[1.0]
print(d)
