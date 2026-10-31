# str: length, slicing, membership and repr

print(len('abc'))

print('abc' < 'abd')

print('a' * 0)

print('abc'[::-1])

print('abc'[1:])

print('abcdef'[::2])

print('abcdef'[-3:])

print('abcdef'[:-3])

print('abcdef'[10:])

print('abcdef'[1:100])

print('abcdef'[4:1])

print('abcdef'[-100:2])

print('abcdef'[::-2])

print('abcdef'[5:1:-1])

print('a' in 'banana')

print('' in 'a')

print('ab' not in 'abc')

print('x')

print(repr('it\'s'))

print(repr('say "hi"'))

print(repr('a\nb\tc'))

print(repr('\\'))

print(repr('é'))

print(repr('\x00'))

print(repr('\x7f'))

print(str(None))

print(str(True))

print(str(1.0))

print(str([1, 'a']))

print(str((1,)))

print(str(()))

print(str({}))

print(str({'a': [1, {'b': 2}]}))
