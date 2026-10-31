a = frozenset([1, 2])
b = frozenset([2, 1])
print(a == b, hash(a) == hash(b))
d = {a: 'x'}
print(d[b], b in d)
s = {a, b, frozenset([3])}
print(len(s))
print(frozenset([frozenset([1]), frozenset([2])]) == frozenset([frozenset([2]), frozenset([1])]))
try:
    hash({1})
except TypeError as e:
    print('TypeError')
try:
    frozenset([[1]])
except TypeError as e:
    print('TypeError')
