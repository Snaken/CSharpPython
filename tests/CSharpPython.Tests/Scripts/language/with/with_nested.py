class CM:
    def __init__(self, n):
        self.n = n
    def __enter__(self):
        print('in', self.n)
        return self
    def __exit__(self, t, v, tb):
        print('out', self.n)
with CM(1):
    with CM(2):
        with CM(3):
            print('deep')
    print('mid')
