a = {1, 2, 3, 4}
b = {3, 4, 5}
print(sorted(a.union(b)), sorted(a.union([9], (10,))), sorted(a.union()))
print(sorted(a.intersection(b)), sorted(a.intersection([3, 4, 8], b)), sorted(a.intersection()))
print(sorted(a.difference(b)), sorted(a.difference([1], [2])), sorted(a - b), sorted(b - a))
print(sorted(a.symmetric_difference(b)), sorted(a.symmetric_difference([1, 9])))
print(sorted(a), sorted(b))
