class V:
    def __add__(self, o):
        raise ValueError('nope')
try:
    V() + 1
except ValueError as e:
    print(e)
