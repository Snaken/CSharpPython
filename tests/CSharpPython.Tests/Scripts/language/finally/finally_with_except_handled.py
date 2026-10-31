try:
    raise ValueError('x')
except ValueError:
    print('h')
finally:
    print('f')
print('after')
