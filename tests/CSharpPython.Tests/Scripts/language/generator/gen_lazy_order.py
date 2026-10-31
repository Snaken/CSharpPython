def noisy():
    print('start')
    yield 1
    print('middle')
    yield 2
    print('end')
g = noisy()
print('created')
print(next(g))
print('between')
print(next(g))
print(list(g))
