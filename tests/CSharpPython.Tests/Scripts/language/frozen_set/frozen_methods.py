a = frozenset([1, 2, 3])
for r in (a.union([4]), a.intersection([2, 3, 9]), a.difference([1]), a.symmetric_difference([3, 4]), a.copy()):
    print(type(r).__name__, sorted(r))
print(a.issubset([1, 2, 3, 4]), a.issuperset([1]), a.isdisjoint([7]), a.isdisjoint([1]))
