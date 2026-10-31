class N:
    def __init__(self):
        self.other = None
a = N()
b = N()
a.other = b
b.other = a
print(a.other.other is a, b.other.other is b)
