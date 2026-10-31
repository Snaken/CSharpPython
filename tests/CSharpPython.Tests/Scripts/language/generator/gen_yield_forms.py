def f():
    a = yield
    print('a', a)
    b = yield 1, 2
    print('b', b)
    (yield 3)
    c = (yield 4) + 1
    print('c', c)
    x = y = yield 5
    print(x, y)
g = f()
print(next(g))
print(g.send('A'))
print(g.send('B'))
print(g.send(None))
print(g.send(10))
try:
    g.send('Z')
except StopIteration:
    print('end')
