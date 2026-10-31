class P:
    def __init__(self, x):
        self.x = x
    def __str__(self):
        return 'P(' + str(self.x) + ')'
p = P(3)
print(p)
print(str(p))
print(f'{p}')
print('{}'.format(p))
print(repr(p)[:12])
