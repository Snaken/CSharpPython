def inner():
    yield 1
    yield 2
    return 'inner done'
def outer():
    result = yield from inner()
    print('inner returned', result)
    return 'outer done'
print(list(outer()))
it = outer()
next(it); next(it)
try:
    next(it)
except StopIteration as e:
    print(e.value)
