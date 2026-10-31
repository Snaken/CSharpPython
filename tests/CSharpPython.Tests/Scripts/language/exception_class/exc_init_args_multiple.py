class E(Exception):
    def __init__(self, a, b):
        super().__init__(a, b)
        self.sum = a + b
try:
    raise E(1, 2)
except E as e:
    print(e.sum, e.args, e)
