def inner():
    yield 1
    yield 2
    return 'inner-result'
def outer():
    r = yield from inner()
    print('r =', r)
    yield 3
    yield from [10, 20]
    yield from 'ab'
    yield from (x * 2 for x in range(3))
    yield from range(2)
print(list(outer()))
def chain(*iterables):
    for it in iterables:
        yield from it
print(list(chain([1, 2], (3,), 'x', range(2))))
def nothing():
    r = yield from []
    print(r)
    return
    yield
print(list(nothing()))
