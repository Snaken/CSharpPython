class P:
    def __init__(self, n):
        self.n = n
    def label(self):
        return f'P{self.n}:{self.n * 2}'
print(P(4).label())
