def inner():
    x = yield 'first'
    print('inner got', x)
    y = yield 'second'
    print('inner got', y)
    return 'end'
def outer():
    r = yield from inner()
    print('outer got', r)
    z = yield 'third'
    print('outer got', z)
it = outer()
print(next(it))
print(it.send('A'))
print(it.send('B'))
try:
    it.send('C')
except StopIteration:
    print('finished')
