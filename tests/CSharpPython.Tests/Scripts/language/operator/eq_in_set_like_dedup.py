class K:
    def __init__(self, n):
        self.n = n
    def __eq__(self, other):
        return self.n == other.n
    def __hash__(self):
        return self.n % 3
seen = {}
for n in (1, 4, 7, 1, 4):
    seen[K(n)] = n
print(len(seen), sorted(seen.values()))
