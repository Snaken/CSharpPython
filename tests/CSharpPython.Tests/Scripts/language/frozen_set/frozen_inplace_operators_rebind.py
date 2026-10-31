a = frozenset([1, 2])
b = a
a |= {3}
print(type(a).__name__, sorted(a), sorted(b))
a &= {1, 3}
print(type(a).__name__, sorted(a))
a -= {1}
print(type(a).__name__, sorted(a))
a ^= {3, 9}
print(type(a).__name__, sorted(a))
