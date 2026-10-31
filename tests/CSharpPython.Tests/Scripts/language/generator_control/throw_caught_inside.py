def g():
    try:
        yield 1
    except ValueError as e:
        print('caught', e)
        yield 'recovered'
    yield 'after'
it = g()
print(next(it))
print(it.throw(ValueError('boom')))
print(next(it))
