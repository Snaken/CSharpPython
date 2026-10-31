class A:
    def __init__(self, n):
        self.n = n
    def __str__(self):
        return '#' + str(self.n)
print(A(1), A(2), sep='|')
print(', '.join([str(A(1)), str(A(2))]))
