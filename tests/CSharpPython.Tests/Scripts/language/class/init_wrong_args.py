class V:
    def __init__(self, x):
        self.x = x
for args in [(), (1, 2)]:
    try:
        V(*args)
    except TypeError:
        print('TypeError')
