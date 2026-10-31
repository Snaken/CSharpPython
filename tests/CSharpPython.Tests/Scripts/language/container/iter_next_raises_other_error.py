class A:
    def __iter__(self):
        return self
    def __next__(self):
        raise ValueError('bad')
try:
    for x in A():
        pass
except ValueError as e:
    print(e)
