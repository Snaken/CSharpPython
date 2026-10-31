class Box:
    def __init__(self):
        self.v = 0
def bump(b):
    b.v += 1
b = Box()
bump(b)
bump(b)
print(b.v)
