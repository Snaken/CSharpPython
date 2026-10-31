xs = [1, 2, 3]
xs[1:2] = xs
print(xs)
xs[:] = xs
print(xs)
xs[len(xs):] = xs
print(xs)
ys = [1, 2, 3]
ys[::-1] = ys
print(ys)
