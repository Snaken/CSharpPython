def inner():
    try:
        yield 1
    except ValueError:
        return 'handled'
def outer():
    r = yield from inner()
    print('inner returned', r)
    yield 'after'
    return 'outer done'
g = outer()
next(g)
print(g.throw(ValueError))
try:
    next(g)
except StopIteration as e:
    print('stop', e.value)
