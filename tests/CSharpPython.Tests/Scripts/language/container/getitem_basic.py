class Grid:
    def __init__(self):
        self.cells = {(0, 0): 'a', (1, 2): 'b'}
    def __getitem__(self, key):
        return self.cells[key]
g = Grid()
print(g[0, 0], g[(1, 2)])
try:
    g[5, 5]
except KeyError as e:
    print('KeyError', e)
