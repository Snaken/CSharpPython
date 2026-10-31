a = {1, 2}
b = {1, 2, 3}
print(a.issubset(b), b.issubset(a), b.issuperset(a), a.issuperset(b), a.issubset(a), a.issubset([1, 2, 5]))
print(a.isdisjoint({3}), a.isdisjoint([2, 3]), set().isdisjoint(a))
print(a <= b, a < b, b >= a, b > a, a <= a, a < a, a >= a, a > a, a <= {7}, {7} <= a)
