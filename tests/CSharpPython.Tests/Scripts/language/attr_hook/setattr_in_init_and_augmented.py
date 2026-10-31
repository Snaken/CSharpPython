class A:
    def __init__(self):
        self.n = 0
    def __setattr__(self, name, value):
        print('->', name, value)
        super().__setattr__(name, value)
a = A()
a.n += 5
a.n = a.n * 2
print(a.n)
