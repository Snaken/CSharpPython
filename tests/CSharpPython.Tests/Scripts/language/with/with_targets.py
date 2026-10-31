class CM:
    def __enter__(self):
        return (1, 2)
    def __exit__(self, *a):
        return False
class Box:
    pass
b = Box()
d = {}
l = [0, 0]
with CM() as (p, q):
    print(p, q)
with CM() as b.v:
    print(b.v)
with CM() as d['k']:
    print(d)
with CM() as l[1]:
    print(l)
with CM() as [m, n]:
    print(m + n)
