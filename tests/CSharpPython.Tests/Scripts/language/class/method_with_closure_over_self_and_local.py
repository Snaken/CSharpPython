class A:
    def __init__(self):
        self.k = 2
    def make(self, add):
        def inner(x):
            return x * self.k + add
        return inner
print(A().make(1)(5))
