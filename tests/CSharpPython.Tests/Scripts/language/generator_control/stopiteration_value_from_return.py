def g():
    yield 1
    return 'result'
it = g()
next(it)
try:
    next(it)
except StopIteration as e:
    print('StopIteration', e.value, e.args)
try:
    next(it)
except StopIteration as e:
    print(e.value)
def h():
    return 5
    yield
try:
    next(h())
except StopIteration as e:
    print(e.value)
