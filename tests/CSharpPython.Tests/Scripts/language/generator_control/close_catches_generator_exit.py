def g():
    try:
        yield 1
    except GeneratorExit:
        print('got GeneratorExit')
        raise
it = g()
next(it)
it.close()
def h():
    try:
        yield 1
    except GeneratorExit:
        print('swallowed')
it = h()
next(it)
it.close()
print('closed')
