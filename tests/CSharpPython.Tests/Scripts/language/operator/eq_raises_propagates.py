class K:
    def __eq__(self, other):
        raise ValueError('no compare')
try:
    K() == K()
except ValueError as e:
    print(e)
try:
    K() in [K()]
except ValueError as e:
    print('in', e)
