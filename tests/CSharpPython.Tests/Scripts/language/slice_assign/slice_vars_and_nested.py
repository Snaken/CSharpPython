grid = [[0] * 3 for _ in range(2)]
grid[0][1:] = [5, 6, 7]
grid[1][:2] = []
print(grid)
a, b = 1, 3
xs = list(range(6))
xs[a:b] = ['m']
print(xs)
def f(l):
    l[1:-1] = ['mid']
ys = [1, 2, 3, 4]
f(ys)
print(ys)
