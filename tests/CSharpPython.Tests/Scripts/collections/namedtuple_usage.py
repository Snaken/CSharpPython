from collections import namedtuple, defaultdict, Counter, deque

Point = namedtuple('Point', 'x y')
Segment = namedtuple('Segment', 'start end')

def length_squared(seg):
    dx = seg.end.x - seg.start.x
    dy = seg.end.y - seg.start.y
    return dx * dx + dy * dy

seg = Segment(Point(0, 0), Point(3, 4))
print(seg, length_squared(seg))
points = [Point(3, 1), Point(1, 2), Point(2, 0)]
print(sorted(points), sorted(points, key=lambda p: p.y), max(points), min(points, key=lambda p: p.x))
print([p.x + p.y for p in points], {p.x: p for p in points}[2])
print(sum(p.x for p in points), list(zip(*points)))
for x, y in points:
    print(x * y, end=' ')
print()
for p in points:
    print(p.x, end=' ')
print()

groups = defaultdict(list)
for p in points:
    groups[p.y % 2].append(p)
print(dict(groups))
print(Counter(p.x % 2 for p in points), Counter(points))
q = deque([Point(1, 1)])
q.append(Point(2, 2))
print(q, q.popleft().x)

class Holder:
    def __init__(self):
        self.pos = Point(0, 0)
    def move(self, dx, dy):
        self.pos = self.pos._replace(x=self.pos.x + dx, y=self.pos.y + dy)
        return self.pos

h = Holder()
h.move(1, 2)
print(h.move(3, 4), h.pos)

def describe(*args, **kwargs):
    return args, kwargs

print(describe(*Point(1, 2)), describe(**Point(3, 4)._asdict()))
args = {'x': 5, 'y': 6}
print(Point(**args), Point(*[7, 8]))

state = {'p': Point(1, 2), 'list': [Point(3, 4)]}
state['p'] = state['p']._replace(y=99)
print(state)
print(Point(1, 2) in {Point(1, 2): 1}, Point(1, 2) in {(1, 2)}, {Point(1, 2), (1, 2)})
a = Point(1, 2)
b = a
print(a is b, a == b, a._replace(x=1) is a, a._replace(x=1) == a)
