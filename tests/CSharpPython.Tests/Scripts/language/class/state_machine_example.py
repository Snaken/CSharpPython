class Light:
    ORDER = ['green', 'yellow', 'red']
    def __init__(self):
        self.i = 0
    def state(self):
        return Light.ORDER[self.i]
    def next(self):
        self.i = (self.i + 1) % len(Light.ORDER)
        return self.state()
l = Light()
print([l.next() for _ in range(4)])
