class R:
    def __init__(self, v):
        self.v = v
    def __rand__(self, o):
        return ('rand', o, self.v)
    def __ror__(self, o):
        return ('ror', o, self.v)
    def __rxor__(self, o):
        return ('rxor', o, self.v)
    def __rlshift__(self, o):
        return ('rlshift', o, self.v)
    def __rrshift__(self, o):
        return ('rrshift', o, self.v)
r = R(7)
print(1 & r, 2 | r, 3 ^ r, 4 << r, 5 >> r)
try:
    r & 1
except TypeError as e:
    print('TypeError', e)
try:
    r | r
except TypeError as e:
    print('TypeError', e)
try:
    ~r
except TypeError as e:
    print('TypeError', e)
