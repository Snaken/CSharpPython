class A: pass
class B: pass
d = {A: 1, B: 2}
print(d[A], d[B], A in d, len(d))
