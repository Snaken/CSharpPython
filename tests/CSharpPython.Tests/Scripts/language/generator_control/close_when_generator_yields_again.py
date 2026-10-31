def g():
    try:
        yield 1
    except GeneratorExit:
        yield 2
it = g()
next(it)
try:
    it.close()
except RuntimeError as e:
    print('RuntimeError')
print(list(it))
