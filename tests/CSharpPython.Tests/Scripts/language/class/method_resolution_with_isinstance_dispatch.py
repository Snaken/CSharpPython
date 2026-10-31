class Shape:
    def area(self):
        return 0
class Sq(Shape):
    def __init__(self, s):
        self.s = s
    def area(self):
        return self.s * self.s
class Rect(Shape):
    def __init__(self, w, h):
        self.w = w
        self.h = h
    def area(self):
        return self.w * self.h
shapes = [Sq(2), Rect(2, 3), Shape()]
print([s.area() for s in shapes], sum(s.area() for s in shapes))
