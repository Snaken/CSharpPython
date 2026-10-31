print(frozenset([1, 2]) == {1, 2}, {1, 2} == frozenset([1, 2]), frozenset([1]) != {1})
print(frozenset([1, 2]) <= {1, 2, 3}, {1} < frozenset([1, 2]), frozenset([1, 2]) >= frozenset([1]))
