def g():
    try:
        yield 1
    finally:
        print('cleanup')
it = g()
next(it)
try:
    it.throw(RuntimeError('fatal'))
except RuntimeError as e:
    print('RuntimeError', e)
print(list(it))
try:
    next(it)
except StopIteration:
    print('done')
