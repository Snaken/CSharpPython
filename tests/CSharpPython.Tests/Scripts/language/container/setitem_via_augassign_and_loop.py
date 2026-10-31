class Counter:
    def __init__(self):
        self.d = {}
    def __getitem__(self, k):
        return self.d.get(k, 0)
    def __setitem__(self, k, v):
        self.d[k] = v
c = Counter()
for ch in 'abca':
    c[ch] += 1
print(c['a'], c['b'], c['c'], c['z'])
