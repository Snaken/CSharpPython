class Count:
    def __init__(self):
        self.i = 0
    def __iter__(self):
        return self
    def __next__(self):
        self.i += 1
        if self.i > 2:
            raise StopIteration
        return self.i
c = Count()
print(next(c), next(c), next(c, 'done'))
it = iter([1, 2])
print(next(it), next(it), next(it, None))
try:
    next(it)
except StopIteration:
    print('stop')
print(list(iter(Count())))
