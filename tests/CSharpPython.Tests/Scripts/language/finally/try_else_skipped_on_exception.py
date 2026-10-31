try:
    raise ValueError('x')
except ValueError:
    print('h')
else:
    print('else')
print('end')
