from itertools import product, permutations, combinations, combinations_with_replacement
import itertools

print(list(product('ab', range(2))), list(product([1, 2])), list(product()), list(product([], [1])), list(product('ab', repeat=2)))
print(list(product(range(2), repeat=3)), list(product([1], repeat=0)), list(product('xy', 'z', [0, 1])), len(list(product(range(4), range(5), range(3)))))
print(list(permutations('abc')), list(permutations(range(3), 2)), list(permutations([1, 2], 3)), list(permutations([])), list(permutations('ab', 0)))
print(list(permutations(range(4), 3))[:6], len(list(permutations(range(5)))), len(list(permutations(range(5), 2))))
print(list(combinations('abcd', 2)), list(combinations(range(4), 3)), list(combinations('ab', 3)), list(combinations('abc', 0)), list(combinations([], 0)), list(combinations(range(3), 1)))
print(list(combinations_with_replacement('abc', 2)), list(combinations_with_replacement(range(2), 3)), list(combinations_with_replacement('', 2)), list(combinations_with_replacement('ab', 0)), list(combinations_with_replacement('', 0)))
print(len(list(combinations(range(10), 4))), len(list(combinations_with_replacement(range(5), 3))))
p = product('ab', 'cd')
print(next(p), next(p), list(p), list(p))
c = combinations(range(4), 2)
print(next(c), list(c), next(c, 'end'))
for name, bad in (('neg', lambda: permutations('ab', -1)), ('neg2', lambda: combinations('ab', -1)), ('r', lambda: combinations('ab', 'x')), ('rep', lambda: product('ab', repeat=-1)),
                  ('it', lambda: product(5)), ('kw', lambda: product('a', other=1)), ('perm', lambda: permutations(5))):
    try:
        bad()
    except (TypeError, ValueError) as e:
        print(name, type(e).__name__)
seen = set()
for combo in permutations('abc'):
    seen.add(''.join(combo))
print(sorted(seen))
grid = [(x, y) for x, y in product(range(3), range(2)) if x != y]
print(grid, sum(1 for _ in product(range(10), repeat=3)))
print(list(itertools.chain(combinations('abc', 2), permutations('ab'))))
print(type(product()).__name__, type(permutations('a')).__name__, type(combinations('a', 1)).__name__, type(combinations_with_replacement('a', 1)).__name__)
