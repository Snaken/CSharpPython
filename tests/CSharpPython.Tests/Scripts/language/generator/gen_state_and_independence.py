def count():
    n = 0
    while True:
        n += 1
        yield n
a = count()
b = count()
print(next(a), next(a), next(b), next(a), next(b))
it = iter(a)
print(it is a, next(it))
def args(a, b=2, *rest, k=5, **kw):
    yield a
    yield b
    yield rest
    yield k
    yield kw
print(list(args(1)))
print(list(args(1, 3, 4, 5, k=6, z=7)))
