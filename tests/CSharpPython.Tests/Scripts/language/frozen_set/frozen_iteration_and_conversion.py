f = frozenset('abca')
print(sorted(f), sorted(set(f)), sorted(list(f)), sorted(tuple(f)), len(set(f)))
print(sorted(x for x in f), max(f), min(f), sum(frozenset([1, 2, 3])), sorted(map(str, f)))
print(type(set(f)).__name__, type(frozenset(set(f))).__name__)
