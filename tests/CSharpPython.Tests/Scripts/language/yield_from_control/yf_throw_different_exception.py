def inner():
    try:
        yield 1
    except ValueError:
        raise ZeroDivisionError('converted')
def outer():
    try:
        yield from inner()
    except ZeroDivisionError as e:
        print('outer got', e)
        yield 'ok'
g = outer()
next(g)
print(g.throw(ValueError))
