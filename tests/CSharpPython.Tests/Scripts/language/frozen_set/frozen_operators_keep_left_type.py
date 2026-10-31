a = frozenset([1, 2, 3])
b = {3, 4}
for r in (a | b, a & b, a - b, a ^ b, b | a, b & a, b - a, b ^ a):
    print(type(r).__name__, sorted(r))
