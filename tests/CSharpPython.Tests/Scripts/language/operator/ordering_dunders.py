class V:
    def __init__(self, n):
        self.n = n
    def __lt__(self, other):
        return self.n < other.n
    def __le__(self, other):
        return self.n <= other.n
    def __gt__(self, other):
        return self.n > other.n
    def __ge__(self, other):
        return self.n >= other.n
a, b = V(1), V(2)
print(a < b, a <= b, a > b, a >= b, b > a, a <= a, a >= a, a < a)
