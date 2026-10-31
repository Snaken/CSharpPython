class Point:
    def __init__(self, x, y):
        self.x, self.y = x, y
    @classmethod
    def origin(cls):
        return cls(0, 0)
    @classmethod
    def parse(cls, s):
        x, y = s.split(',')
        return cls(int(x), int(y))
    def __repr__(self):
        return 'Point(%d, %d)' % (self.x, self.y) if False else 'Point(' + str(self.x) + ', ' + str(self.y) + ')'
print(Point.origin(), Point.parse('3,4'), Point(1, 2).origin())
