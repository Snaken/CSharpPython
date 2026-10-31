d = {'a': 1, 'b': 2, 'c': 3}
e = {'b': 2, 'c': 30, 'd': 4}
print(sorted(d.keys() | e.keys()), sorted(d.keys() & e.keys()), sorted(d.keys() - e.keys()), sorted(d.keys() ^ e.keys()))
print(sorted(d.keys() | ['x', 'a']), sorted(['x', 'a'] | d.keys()), sorted(d.keys() & {'a', 'q'}), sorted({'a', 'q'} & d.keys()))
print(sorted(d.keys() - ['a']), sorted(['a', 'z'] - d.keys()), sorted(d.keys() ^ 'abz'), sorted('abz' ^ d.keys()))
print(sorted(d.items() & e.items()), sorted(d.items() | e.items()), sorted(d.items() - e.items()))
print(type(d.keys() | e.keys()).__name__, type(d.items() & e.items()).__name__)
print(len(d.keys() | (1, 2)), sorted(d.keys() & iter(['a'])), len(d.keys() | range(3)))
s = {'a', 'zz'}
s |= d.keys()
print(sorted(s))
x = d.keys()
x |= {'new'}
print(sorted(x), type(x).__name__)
print(sorted({1, 2} | {1: 0, 5: 0}.keys()))
