def inner():
    try:
        yield 1
    except ValueError as e:
        print('inner caught', e)
        yield 100
    yield 2
    return 'r'
def outer():
    r = yield from inner()
    print('outer got', r)
    yield 3
g = outer()
print(next(g))
print(g.throw(ValueError, 'boom'))
print(next(g))
print(next(g))
try:
    next(g)
except StopIteration:
    print('done')
