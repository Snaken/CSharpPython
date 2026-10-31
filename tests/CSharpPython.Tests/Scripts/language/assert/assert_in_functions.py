def positive(n):
    assert n > 0, 'n must be positive'
    return n
print(positive(3))
try:
    positive(-1)
except AssertionError as e:
    print(e)
def checker(items):
    for i in items:
        assert i % 2 == 0, str(i) + ' is odd'
    return 'ok'
print(checker([2, 4]))
try:
    checker([2, 3, 4])
except AssertionError as e:
    print(e)
class V:
    def __init__(self, n):
        assert n >= 0, 'negative'
        self.n = n
print(V(1).n)
try:
    V(-1)
except AssertionError as e:
    print(e)
