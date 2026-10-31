class M:
    def f(self, a, b=2, *rest, k=3, **kw):
        return (a, b, rest, k, kw)
m = M()
print(m.f(1), m.f(1, 5, 6, 7, k=0, z=1))
