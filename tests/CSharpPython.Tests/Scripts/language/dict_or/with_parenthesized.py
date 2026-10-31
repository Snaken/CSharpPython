class R:
    def __init__(self, n): self.n = n
    def __enter__(self):
        print('enter', self.n)
        return self.n
    def __exit__(self, *a):
        print('exit', self.n)
        return False
with (R(1) as a, R(2) as b):
    print(a, b)
with (R(3) as a,
      R(4) as b,):
    print(a + b)
with (R(5), R(6)):
    print('both')
with (R(7)) as z:
    print(z)
with (R(8)):
    print('one')
with (R(9) as q):
    print(q)
with (R(10) as q, R(11)):
    print(q)
with (R(12)), R(13) as w:
    print(w)
