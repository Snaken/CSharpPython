class A:
    def __init__(self, v):
        self.v = v
    def __bool__(self):
        return self.v > 0
print(bool(A(1)), bool(A(0)), not A(0), 'y' if A(2) else 'n', A(0) or 'fallback', A(1) and 'both')
n = 0
while A(3 - n):
    n += 1
print(n)
