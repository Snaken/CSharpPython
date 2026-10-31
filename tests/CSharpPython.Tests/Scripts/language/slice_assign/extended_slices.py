xs = list(range(10))
xs[::2] = ['e'] * 5
print(xs)
xs[1::3] = [100, 200, 300]
print(xs)
xs[::-1] = list(range(10))
print(xs)
ys = [1, 2, 3, 4]
ys[3:0:-2] = ['p', 'q']
print(ys)
ys[::2] = 'zz'
print(ys)
