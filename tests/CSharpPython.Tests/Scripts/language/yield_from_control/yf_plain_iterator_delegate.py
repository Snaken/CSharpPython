def outer():
    try:
        yield from [1, 2, 3]
    except ValueError as e:
        print('outer caught', e)
        yield 'x'
g = outer()
print(next(g))
print(g.throw(ValueError('v')))
g = outer()
next(g)
g.close()
print('closed')
def outer2():
    try:
        yield from iter([1, 2])
    finally:
        print('fin')
g = outer2()
next(g)
try:
    g.throw(ZeroDivisionError('k'))
except ZeroDivisionError as e:
    print('escaped', e)
