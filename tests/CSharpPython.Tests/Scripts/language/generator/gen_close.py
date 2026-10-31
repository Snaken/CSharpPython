def f():
    yield 1
    yield 2
g = f()
print(next(g))
g.close()
try:
    next(g)
except StopIteration:
    print('closed')
g.close()
print(list(f()))
