import itertools
from itertools import count, cycle, repeat, chain, islice, accumulate, compress, dropwhile, takewhile, filterfalse, starmap, zip_longest, pairwise, batched, groupby
import operator

print(list(islice(count(), 5)), list(islice(count(10, 3), 4)), list(islice(count(1.5, 0.5), 3)), list(islice(count(5, -2), 4)), list(islice(count(step=2), 3)))
print(list(islice(cycle('abc'), 8)), list(cycle([])), list(islice(cycle(range(2)), 5)))
print(list(repeat('x', 3)), list(repeat(1, 0)), list(repeat(1, -2)), list(islice(repeat(7), 3)), list(repeat(times=2, object=5)))
print(list(chain([1, 2], (3,), 'ab', range(2))), list(chain()), list(chain([], [])), list(chain.from_iterable([[1], [2, 3], []])))
print(list(chain.from_iterable(x for x in ([i] * i for i in range(4)))))
print(list(islice('abcdefg', 3)), list(islice('abcdefg', 2, 4)), list(islice('abcdefg', 2, None)), list(islice('abcdefg', 0, None, 3)), list(islice(range(10), 1, 8, 3)))
print(list(islice('abc', 0)), list(islice('abc', 10)), list(islice(count(), 3, 3)), list(islice(range(5), None)))
it = iter(range(10))
print(list(islice(it, 3)), list(islice(it, 2)), next(it))
print(list(accumulate([1, 2, 3, 4])), list(accumulate([1, 2, 3, 4], operator.mul)), list(accumulate([3, 1, 4, 1, 5], max)), list(accumulate([1, 2, 3], initial=10)))
print(list(accumulate([], initial=5)), list(accumulate([])), list(accumulate('abc')), list(accumulate([1, 2, 3], lambda a, b: a * 10 + b)), list(accumulate([1, 2], func=operator.sub, initial=0)))
print(list(compress('abcdef', [1, 0, 1, 0, 1, 1])), list(compress('abc', [1])), list(compress([1, 2], [0, 0])))
print(list(dropwhile(lambda x: x < 3, [1, 2, 3, 1, 4])), list(takewhile(lambda x: x < 3, [1, 2, 3, 1, 4])), list(dropwhile(bool, [])), list(takewhile(bool, [1, 0, 1])))
print(list(filterfalse(lambda x: x % 2, range(8))), list(filterfalse(None, [0, 1, '', 'a', None, 2])))
print(list(starmap(pow, [(2, 3), (3, 2)])), list(starmap(lambda a, b, c: a + b + c, [(1, 2, 3), [4, 5, 6]])), list(starmap(max, [])))
print(list(zip_longest('ab', 'xyz')), list(zip_longest('ab', 'xyz', fillvalue='-')), list(zip_longest()), list(zip_longest([], [], fillvalue=0)), list(zip_longest([1], fillvalue=9)))
print(list(zip_longest(range(3), 'a', [True, False, None, 1])))
print(list(pairwise('abcd')), list(pairwise([1])), list(pairwise([])), list(pairwise(range(4))))
print(list(batched('abcdefg', 3)), list(batched(range(4), 2)), list(batched([], 3)), list(batched('abc', 5)))
for strict in (False, True):
    try:
        print(list(batched('abcde', 2, strict=strict)))
    except ValueError as e:
        print('ValueError', e)
for key in (None, len, str.lower, lambda x: x % 3):
    data = ['a', 'B', 'b', 'cc', 'dd', 'e', 'Ff'] if key is not len and key is not str.lower and key is not None else ['a', 'B', 'b', 'cc', 'dd', 'e', 'Ff']
    if key is not None and key.__class__.__name__ == 'function':
        data = [1, 4, 7, 2, 5, 3, 6, 9]
    print([(k, list(g)) for k, g in groupby(data, key)])
print([(k, len(list(g))) for k, g in groupby('AAAABBBCCDAABBB')], [k for k, g in groupby('')])
groups = groupby('aabbb')
k1, g1 = next(groups)
k2, g2 = next(groups)
print(k1, k2, list(g1), list(g2))
groups = groupby([1, 1, 2, 3, 3])
first = next(groups)
second = next(groups)
print(first[0], list(first[1]), second[0], [x for x in second[1]], [(k, list(g)) for k, g in groups])
print(sum(islice(count(1), 100)), list(zip('abc', count(1))), max(islice(cycle([3, 1, 2]), 7)), sorted(chain([3, 1], [2])))
total = 0
for a, b in zip(count(), 'xyz'):
    total += a
print(total)

# Errors
for bad in (lambda: list(islice('abc', -1)), lambda: list(islice('abc', 1, 2, 0)), lambda: list(islice('abc', 'x')), lambda: batched('ab', 0), lambda: count('a'), lambda: list(chain(1)),
            lambda: list(accumulate(5)), lambda: list(takewhile(1, [1])), lambda: list(starmap(max, [1])), lambda: repeat(), lambda: count(foo=1), lambda: zip_longest(bad=1)):
    try:
        bad()
    except (TypeError, ValueError) as e:
        print(type(e).__name__)
print(type(count()).__name__, type(chain()).__name__, type(islice([], 1)).__name__, type(groupby([])).__name__)
