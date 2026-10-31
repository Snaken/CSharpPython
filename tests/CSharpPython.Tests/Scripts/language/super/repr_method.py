class P:
    def __init__(self, x):
        self.x = x
    def __repr__(self):
        return 'P<' + str(self.x) + '>'
p = P(3)
print(repr(p))
print(str(p))
print(p)
print([p, P(4)])
print({'k': p})
print((p,))
print(f'{p!r} {p!s}')
