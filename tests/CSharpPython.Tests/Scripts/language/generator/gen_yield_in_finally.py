def f():
    try:
        yield 1
    finally:
        yield 'cleanup'
print(list(f()))
def g():
    try:
        raise ValueError('pending')
    finally:
        yield 'in finally'
it = g()
print(next(it))
try:
    next(it)
except ValueError as e:
    print('ValueError', e)
def h():
    for i in range(2):
        try:
            if i == 0:
                raise KeyError('k')
            yield 'normal'
        except KeyError:
            yield 'handler'
        finally:
            yield 'fin' + str(i)
print(list(h()))
