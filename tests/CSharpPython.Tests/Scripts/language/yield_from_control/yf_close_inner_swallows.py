def inner():
    try:
        yield 1
    except GeneratorExit:
        print('inner exit')
        return 'x'
def outer():
    try:
        r = yield from inner()
        print('not reached', r)
        yield 2
    finally:
        print('outer finally')
g = outer()
next(g)
g.close()
print('closed')
try:
    next(g)
except StopIteration:
    print('done')
