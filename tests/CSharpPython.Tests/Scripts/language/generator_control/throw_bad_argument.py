def g():
    yield 1
it = g()
next(it)
try:
    it.throw(5)
except TypeError:
    print('TypeError')
try:
    it.throw(int)
except TypeError:
    print('TypeError')
