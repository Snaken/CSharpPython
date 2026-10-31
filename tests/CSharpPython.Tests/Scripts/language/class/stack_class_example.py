class Stack:
    def __init__(self):
        self.items = []
    def push(self, x):
        self.items.append(x)
    def pop(self):
        if not self.items:
            raise IndexError('empty')
        return self.items.pop()
    def __len__(self):
        return len(self.items)
s = Stack()
s.push(1)
s.push(2)
print(s.pop(), s.pop())
try:
    s.pop()
except IndexError as e:
    print('IndexError', e)
