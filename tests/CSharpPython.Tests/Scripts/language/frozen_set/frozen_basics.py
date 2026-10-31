f = frozenset([3, 1, 2, 3])
print(len(f), sorted(f), 2 in f, 5 in f, type(f).__name__, frozenset())
print(frozenset([1]), repr(frozenset()), bool(frozenset()), bool(f))
print(isinstance(f, frozenset), isinstance(f, set), isinstance(set(), frozenset), type(f) == frozenset, type(set()) == set)
