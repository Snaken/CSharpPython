def inner():
    yield 1
def outer():
    yield from inner()
    try:
        yield 2
    except ValueError:
        print('after delegation')
        yield 3
g = outer()
next(g)
next(g)
print(g.throw(ValueError))
