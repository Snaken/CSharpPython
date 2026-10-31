class Box:
    pass
bx = Box()
xs = [0, 0, 0]
bx.first, *xs[1:], = [10, 20, 30, 40]
print(bx.first, xs)
d = {}
d['a'], *d['rest'] = 'abc'
print(d)
