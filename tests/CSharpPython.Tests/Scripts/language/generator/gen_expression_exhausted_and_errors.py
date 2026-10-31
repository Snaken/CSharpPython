g = (x for x in range(2))
print(list(g), list(g))
try:
    next(g)
except StopIteration:
    print('stop')
h = (1 / x for x in [1, 0])
print(next(h))
try:
    next(h)
except ZeroDivisionError:
    print('zde')
try:
    (x for x in 5)
except TypeError as e:
    print('TypeError', e)
