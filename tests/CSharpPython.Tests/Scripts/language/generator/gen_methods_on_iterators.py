it = iter([1, 2, 3])
print(it.__next__(), next(it))
def f():
    yield 5
g = f()
print(g.__next__())
try:
    g.__next__()
except StopIteration:
    print('stop')
print(list(g.__iter__()))
