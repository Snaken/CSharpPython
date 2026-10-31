try:
    next(iter([]))
except StopIteration as e:
    print(e.value, e.args)
try:
    raise StopIteration
except StopIteration as e:
    print(e.value)
def g():
    yield
it = g()
list(it)
try:
    next(it)
except StopIteration as e:
    print(e.value)
