# how lists, tuples, dicts and sets print

print((1,) + (2,))

print([] + [])

print([1] + [[2]])

print('a' + 'b')

print((1, 2) * 2)

print({'a': 1, 'b': 2})

print({1: 'a', 2: 'b'})

print({'a': 1}['a'])

print({(1, 2): 3}[(1, 2)])

print({1: 'x', 1.0: 'y'})

print({True: 'x', 1: 'y'})

print({'a': {'b': 1}}['a']['b'])

print({None: 1})

print({(1, 'a'): [1, 2]})

print({1: 2}.get(1))

print({'a': 1, 'a': 2})

print({'b': 1, 'a': 2})

print([[1, 2], [3]])

print([(1, 2), (3,)])

print([None, True, 1.5, 'a'])

print([[]])

print(([], {}, ()))

print([1, [2, [3, [4]]]])

print(({'a': (1, [2])},))

print([{}])

print(((),))

print([()])

print([1.0, -0.0, float('inf')])

xs = [3, 1, 2]
xs.append(4)
print(xs)

xs = [1]
xs.extend([2, 3])
xs.extend((4,))
xs.extend('ab')
xs.extend(range(2))
print(xs)
