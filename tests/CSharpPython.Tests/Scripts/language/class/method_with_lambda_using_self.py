class A:
    def __init__(self):
        self.k = 3
    def make(self):
        return lambda x: x * self.k
print(A().make()(5))
