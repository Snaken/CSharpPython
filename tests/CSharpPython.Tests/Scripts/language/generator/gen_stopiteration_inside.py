def f():
    yield 1
    raise StopIteration
try:
    print(list(f()))
except RuntimeError as e:
    print('RuntimeError', e)
def g():
    yield 1
    next(iter([]))
try:
    print(list(g()))
except RuntimeError as e:
    print('RuntimeError', e)
