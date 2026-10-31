def g():
    while True:
        try:
            yield 'waiting'
        except IndexError as e:
            print('IndexError', e)
        except ValueError as e:
            print('ValueError', e)
it = g()
next(it)
it.throw(IndexError, 'k')
it.throw(ValueError, 'v')
it.throw(ValueError())
print(next(it))
