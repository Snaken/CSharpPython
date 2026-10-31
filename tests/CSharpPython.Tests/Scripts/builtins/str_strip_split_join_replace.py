# str: strip, case, split, join and replace

print('Hello'.upper())

print('Hello'.lower())

print('  hi  '.strip())

print('xxhixx'.strip('x'))

print('  hi  '.lstrip())

print('  hi  '.rstrip())

print('xxhixx'.lstrip('x'))

print('xxhixx'.rstrip('x'))

print('a,b,,c'.split(','))

print('a b  c'.split())

print('  a b '.split())

print('a,b,c'.split(',', 1))

print('abc'.split('b'))

print(''.split())

print(''.split(','))

print('a  b'.split(' '))

print('a,b,c'.split(',', 0))

print('a b c d'.split(None, 2))

print(','.join(['a','b','c']))

print(''.join([]))

print('-'.join(('x','y')))

print('-'.join('abc'))

print('banana'.replace('a','o'))

print('banana'.replace('a','o',2))

print('abc'.replace('','-'))

print('aaa'.replace('aa','b'))

print('hello'.startswith('he'))
