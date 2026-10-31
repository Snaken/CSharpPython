class T:
    def __index__(self): return True
print([5, 6][T()])
