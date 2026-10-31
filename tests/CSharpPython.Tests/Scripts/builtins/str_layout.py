# str: capitalize, title, justify, zfill and splitlines

print('hello world'.capitalize())

print('HELLO'.capitalize())

print('hello world'.title())

print("they're here".title())

print('5'.zfill(3))

print('-5'.zfill(4))

print('+5'.zfill(4))

print('abc'.zfill(2))

print('ab'.ljust(5) + '|')

print('ab'.rjust(5))

print('ab'.center(6) + '|')

print('ab'.center(5, '*'))

print('ab'.ljust(5, '.'))

print('ab'.rjust(5, '0'))

print('abc'.center(2))

print('a\nb\r\nc'.splitlines())

print('a\n'.splitlines())

print(''.splitlines())

print('a\nb'.splitlines(True))

print('{} and {}'.format(1, 'x'))

print('{0}{1}{0}'.format('a','b'))
