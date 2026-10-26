# any, all, sum, min and max read their input one item at a time.
seen = []


def numbers(limit):
    for i in range(limit):
        seen.append(i)
        yield i


print(any(n > 2 for n in numbers(100)), seen)
seen.clear()
print(all(n < 3 for n in numbers(100)), seen)
seen.clear()
print(any(numbers(1)), seen, all(numbers(1)), any([]), all([]), any(()), all(''))


def forever():
    n = 0
    while True:
        yield n
        n += 1


print(any(n > 1000 for n in forever()), all(n < 1000 for n in forever()))

print(sum(range(10 ** 7)), sum(range(10, 0, -3)), sum(range(5), 100))
print(sum(n for n in numbers(5)), sum([0.1] * 10), sum([0.1, 0.2, 0.3]), sum([1, 2.5, 3]))
print(sum([1, 2.5, 3], 10), sum([], 0.5), sum([1, 2], 0.5), sum([True, True, 2.0]))
print(sum([[1], [2, 3]], []), sum((a for a in [(1,), (2,)]), ()), sum([2 ** 70, 1]), sum([1.5, 2 ** 70]))
print(sum([float('inf'), 1.0]), sum([1e308, 1e308, -1e308]), sum({1: 'a', 2: 'b'}), sum({3, 4}))
for bad in (["a", "b"], [1, "a"], 5, [None]):
    try:
        sum(bad)
    except TypeError:
        print('TypeError')

print(min(5, 2, 9), max(5, 2, 9), min([4, 1, 3]), max([4, 1, 3]), min('bca'), max('bca'))
print(min([], default=7), max([], default=None), min(range(3, 8)), max(range(3, 8)))
print(min(['bb', 'a', 'cc'], key=len), max(['bb', 'a', 'cc'], key=len))
print(min([(1, 'b'), (1, 'a')]), max([(1, 'b'), (1, 'a')]), max(1, 1.0), max(1.0, 1), min(True, 1), min(1, True))
calls = []


def key(x):
    calls.append(x)
    return -x


print(max(numbers(4), key=key), calls)
print(min(n for n in numbers(5)), max(n * n for n in numbers(5)), min({3: 'a', 1: 'b'}), max({3, 9, 4}))
for call in (lambda: min([]), lambda: max(()), lambda: min(5), lambda: max(1, default=2), lambda: min([1, 'a']),
             lambda: max(1, 2, foo=3), lambda: min(), lambda: max([1], [2], default=0)):
    try:
        call()
    except (TypeError, ValueError) as e:
        print(type(e).__name__)
