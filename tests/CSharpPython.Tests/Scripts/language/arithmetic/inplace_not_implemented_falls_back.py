class V:
    def __iadd__(self, o):
        return NotImplemented
    def __add__(self, o):
        return 'via add'
v = V()
v += 1
print(v)
