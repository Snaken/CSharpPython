class A:
    def __init__(self):
        self.items = [1, 2, 3]
    def total(self):
        return sum(self.items)
    def __str__(self):
        return 'total=' + str(self.total())
print(A())
