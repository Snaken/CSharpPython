# len, list, tuple, dict, sorted, reversed, enumerate, zip, any and all

print(len([1, 2]))

print(len(()))

print(len({}))

print(len(range(5)))

print(len(''))

print(len({1: 1, 2: 2}))

print(len(range(0, 10, 3)))

print(len(range(10, 0, -3)))

print(list('abc'))

print(list((1, 2)))

print(list({'a': 1}))

print(list(range(3)))

print(list())

print(list(range(5, 0, -2)))

print(list(range(0)))

print(list([1, [2]]))

print(tuple('ab'))

print(tuple([1, 2]))

print(tuple())

print(tuple(range(2)))

print(tuple({1: 2}))

print(dict())

print(dict([(1, 2), (3, 4)]))

print(dict([['a', 1]]))

print(dict({'a': 1}))

print(dict(a=1, b=2))

print(dict([('a', 1)], b=2))

print(dict(zip('ab', [1, 2])))

print(dict([]))

print(sorted([3, 1, 2]))

print(sorted('bca'))

print(sorted([3, 1, 2], reverse=True))

print(sorted((2, 1)))

print(sorted({'b': 1, 'a': 2}))

print(sorted([1.5, 1, True]))

print(sorted([]))

print(sorted(['b', 'B', 'a']))

print(sorted([(2, 'a'), (1, 'b'), (1, 'a')]))

print(sorted([[2], [1, 5], [1]]))

print(sorted(range(3), reverse=True))

print(sorted([3, 1, 2], reverse=False))

print(list(reversed([1, 2, 3])))

print(list(reversed('abc')))

print(list(reversed(range(3))))

print(list(reversed((1, 2))))

print(list(enumerate(['a', 'b'])))

print(list(enumerate('ab', 1)))

print(list(enumerate([])))

print(list(enumerate(['a'], start=5)))

print(list(zip([1, 2], 'ab')))

print(list(zip([1, 2, 3], 'ab')))

print(list(zip()))

print(list(zip([1])))

print(list(zip([1, 2], [3, 4], [5, 6])))

print(list(zip(range(3), 'abc')))

print(list(zip('', [1])))

print(any([0, 1]))

print(any([]))

print(any([0, '']))

print(all([1, 2]))

print(all([]))

print(all([1, 0]))

print(any(range(3)))

print(all('ab'))

print(all(range(1, 3)))

print(any({0: 1}))

print(any((0, 0, 3)))

print([1, 2, 3][0])

print([1, 2, 3][-1])
