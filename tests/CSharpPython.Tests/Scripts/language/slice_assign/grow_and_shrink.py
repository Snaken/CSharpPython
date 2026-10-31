xs = [0, 1, 2, 3, 4, 5]
xs[1:3] = ['a', 'b', 'c', 'd']
print(xs)
xs[1:5] = []
print(xs)
xs[2:2] = ['ins']
print(xs)
xs[len(xs):] = [7, 7]
print(xs)
xs[:] = [1]
print(xs)
