def g():
    yield 1
it = g()
try:
    it.throw(ValueError('early'))
except ValueError as e:
    print('ValueError', e)
print(list(it))
it2 = g()
list(it2)
try:
    it2.throw(KeyError('late'))
except KeyError as e:
    print('KeyError')
