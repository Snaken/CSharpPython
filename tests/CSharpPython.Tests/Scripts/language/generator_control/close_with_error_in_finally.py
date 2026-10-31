def g():
    try:
        yield 1
    finally:
        raise ValueError('from finally')
it = g()
next(it)
try:
    it.close()
except ValueError as e:
    print('ValueError', e)
