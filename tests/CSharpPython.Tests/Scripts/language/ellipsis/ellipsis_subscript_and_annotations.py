class Grid:
    def __getitem__(self, key):
        return key
g = Grid()
print(g[...], g[..., 1], g[1, ...], g[...:2] if False else 'skip')
def f(a: tuple[int, ...] = (1, 2), *rest: ...) -> ...:
    return a
x: tuple[int, ...] = (1,)
print(f(), x)
