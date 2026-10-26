f = lambda a, /, b: a + b
print(f(1, 2), f(1, b=2))
class C:
    def m(self, x, /, y):
        return (x, y)
c = C()
print(c.m(1, 2), c.m(1, y=2))
try:
    c.m(x=1, y=2)
except TypeError:
    print('TypeError')
print(sorted([3, 1, 2], key=lambda v, /: -v))
