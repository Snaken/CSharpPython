# Sorting and reducing lists of ints, floats and strings take a short cut; the results must not differ.
print(sorted([5, 3, 9, 1, 3, 7, 1]), sorted([5, 3, 9, 1], reverse=True), sorted([]), sorted([4]))
print(sorted([2.5, -1.0, 0.0, -0.0, 3.25, 1e300, -1e300, float('inf'), float('-inf')]))
print(sorted(['pear', 'Apple', 'apple', '', 'zebra', 'Zebra', 'a', 'ab']))
print(sorted(['b', 'a', 'c'], reverse=True), sorted(['é', 'e', 'f', 'z']))

pairs = [(1, 'a'), (0, 'b'), (1, 'c'), (0, 'd'), (1, 'e'), (2, 'f'), (0, 'g')]
print([p[1] for p in sorted(pairs, key=lambda p: p[0])])
print([p[1] for p in sorted(pairs, key=lambda p: p[0], reverse=True)])
print([p[1] for p in sorted(pairs, key=lambda p: -p[0])])
words = ['bb', 'a', 'cc', 'd', 'eee', 'ff', 'g']
print(sorted(words, key=len), sorted(words, key=len, reverse=True))
floats = [(0.0, 'p'), (-0.0, 'q'), (0.0, 'r'), (-0.0, 's')]
print([f[1] for f in sorted(floats, key=lambda f: f[0])], [f[1] for f in sorted(floats, key=lambda f: f[0], reverse=True)])

mixed = [3, 1.5, 2, 2.0, True, 0, False, 1]
print(sorted(mixed), sorted(mixed, reverse=True))
print(sorted([3, float('nan'), 1]) == sorted([3, float('nan'), 1]), sorted([2, 1, 3], key=lambda x: -x))
for bad in ([1, 'a'], [1, None], ['a', 1], [[1], 2]):
    try:
        sorted(bad)
    except TypeError:
        print('TypeError')

big = [2 ** 70, 1, -(2 ** 70), 3]
print(sorted(big), max(big), min(big), sum(big))
data = [(i * 7919) % 1009 for i in range(3000)]
ordered = sorted(data)
print(ordered[:5], ordered[-5:], ordered == sorted(ordered), sorted(data, reverse=True)[:3])
numbers = [3, 1, 2]
numbers.sort(reverse=True)
names = ['b', 'a']
names.sort()
print(numbers, names)

print(sum([1, 2, 3]), sum([2 ** 62, 2 ** 62, 2 ** 62]), sum([-2 ** 62, -2 ** 62, -2 ** 62]), sum([2 ** 63 - 1, 1]))
print(sum([1, 2.5]), sum([True, True]), sum([0.1] * 10), sum([1, 2], 10), sum([[1], [2]], []))
print(max([1, 2 ** 70]), min([2 ** 70, 1]), max([3, 2.5]), min([3, 2.5]), max(2, 2.0), min(2, 2.0), max([True, 1]), max([1, True]))
print(max(['b', 'a']), min(['b', 'a']), max([1, 5, 3], key=lambda x: -x), min([1, 5, 3], key=lambda x: -x))
print(max([(1, 2), (1, 3)]), min(3, 1, 2), max(range(5)), min(range(2, 9)))
