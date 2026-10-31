class A:
    def __str__(self):
        raise ValueError('nope')
try:
    print(A())
except ValueError as e:
    print('caught', e)
