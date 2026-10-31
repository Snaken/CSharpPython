def a():
    try:
        yield 'a1'
    except ValueError:
        print('a caught')
        yield 'a2'
    finally:
        print('a finally')
def b():
    try:
        yield from a()
    finally:
        print('b finally')
def c():
    try:
        yield from b()
    finally:
        print('c finally')
g = c()
print(next(g))
print(g.throw(ValueError))
g.close()
print('closed')
h = c()
next(h)
h.close()
print('closed 2')
k = c()
next(k)
try:
    k.throw(ZeroDivisionError('q'))
except ZeroDivisionError as e:
    print('escaped', e)
