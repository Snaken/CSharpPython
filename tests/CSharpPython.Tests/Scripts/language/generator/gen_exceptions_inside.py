def f():
    try:
        yield 1
        yield 2
    except ValueError:
        yield 'caught'
    finally:
        print('cleanup')
g = f()
print(next(g), next(g))
try:
    next(g)
except StopIteration:
    print('done')
def bad():
    yield 1
    raise ValueError('inside')
g = bad()
print(next(g))
try:
    next(g)
except ValueError as e:
    print('ValueError', e)
try:
    next(g)
except StopIteration:
    print('exhausted after error')
def g2():
    yield 1
    1 / 0
try:
    for x in g2():
        print(x)
except ZeroDivisionError:
    print('zde')
