# str: startswith, endswith, find, index and count

print('hello'.startswith('lo'))

print('hello'.endswith('lo'))

print('hello'.startswith(('x','he')))

print('hello'.endswith(('x','y')))

print('hello'.find('l'))

print('hello'.find('z'))

print('hello'.rfind('l'))

print('hello'.find('l', 3))

print('hello'.find('l', 0, 2))

print('hello'.index('e'))

print('hello'.count('l'))

print('aaaa'.count('aa'))

print('abc'.count(''))

print('123'.isdigit())

print('12a'.isdigit())
