class Seq:
    def __init__(self):
        self.items = [10, 20, 30]
    def __getitem__(self, i):
        return self.items[i]
    def __setitem__(self, i, v):
        self.items[i] = v
    def __len__(self):
        return len(self.items)
s = Seq()
s[1] = 99
s[-1] += 1
print(s[0], s[1], s[2], s[-1], len(s), s[0:2])
