class S:
    def __getitem__(self, k):
        return k
x = S()
print(type(x[1:3]).__name__, x[4], repr(x[::2]))
