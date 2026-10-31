def f():
    try:
        next(g)
    except ValueError as e:
        print('ValueError', e)
    yield 1
g = f()
print(next(g))
