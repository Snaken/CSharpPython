class A:
    def __init__(self):
        self.d = {}
        self.l = [0, 0]
a = A()
a.d['k'] = 1
a.l[1] = 5
a.d['k'] += 1
print(a.d, a.l)
