class Stack:
    def __init__(self):
        self.items = []
    def push(self, x):
        self.items.append(x)
    def __len__(self):
        return len(self.items)
    def __getitem__(self, i):
        return self.items[i]
    def __iter__(self):
        return iter(reversed(self.items))
    def __contains__(self, x):
        return x in self.items
    def __bool__(self):
        return len(self.items) > 0
    def __repr__(self):
        return 'Stack(' + repr(self.items) + ')'
s = Stack()
print(bool(s), len(s))
s.push(1)
s.push(2)
s.push(3)
print(s, bool(s), len(s), s[0], s[-1], list(s), 2 in s, 9 in s)
for x in s:
    print(x, end=' ')
print()
