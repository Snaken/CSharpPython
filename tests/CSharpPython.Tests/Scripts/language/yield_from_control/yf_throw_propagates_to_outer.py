def inner():
    try:
        yield 1
    finally:
        print('inner finally')
def outer():
    try:
        yield from inner()
    except ZeroDivisionError as e:
        print('outer caught', repr(e))
        yield 'recovered'
g = outer()
next(g)
print(g.throw(ZeroDivisionError('k')))
try:
    next(g)
except StopIteration:
    print('done')
g2 = outer()
next(g2)
try:
    g2.throw(TypeError('t'))
except TypeError as e:
    print('escaped', e)
