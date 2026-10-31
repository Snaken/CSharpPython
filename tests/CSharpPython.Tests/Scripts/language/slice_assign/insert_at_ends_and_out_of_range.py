xs = [1, 2, 3]
xs[0:0] = ['a']
xs[100:] = ['end']
xs[-100:-99] = ['start']
print(xs)
xs[3:1] = ['back']
print(xs)
xs[-2:] = 'xy'
print(xs)
