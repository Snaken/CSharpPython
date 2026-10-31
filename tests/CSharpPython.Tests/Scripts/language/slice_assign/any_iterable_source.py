xs = [0, 0, 0]
xs[1:2] = range(3)
print(xs)
xs[:1] = (7, 8)
print(xs)
xs[-1:] = {'k': 1}
print(xs)
xs[:] = (c for c in 'abc')
print(xs)
xs[1:1] = iter([5, 6])
print(xs)
xs[0:0] = {3}
print(xs)
