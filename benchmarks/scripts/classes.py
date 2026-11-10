class Vec:
    def __init__(self, x, y):
        self.x = x
        self.y = y

    def add(self, other):
        return Vec(self.x + other.x, self.y + other.y)

def main():
    v = Vec(0, 0)
    step = Vec(1, 2)
    for i in range(100000):
        v = v.add(step)
    assert v.x == 100000 and v.y == 200000

main()
