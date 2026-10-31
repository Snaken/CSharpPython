class Wrap:
    def __init__(self):
        self.items = [3, 1, 2]
    def __iter__(self):
        return iter(self.items)
w = Wrap()
print(list(w), sorted(w), len(list(w)), [x * 2 for x in w], dict(zip(w, 'abc')))
for x in w:
    print(x)
