def inner():
    while True:
        try:
            yield 1
        except GeneratorExit:
            pass
def outer():
    try:
        yield from inner()
    finally:
        print('outer finally')
g = outer()
next(g)
try:
    g.close()
except RuntimeError as e:
    print('RuntimeError', e)
try:
    next(g)
except StopIteration:
    print('done')
