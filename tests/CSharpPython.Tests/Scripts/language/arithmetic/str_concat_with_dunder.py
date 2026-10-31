class T:
    def __init__(self, s):
        self.s = s
    def __add__(self, o):
        return T(self.s + str(o))
    def __radd__(self, o):
        return T(str(o) + self.s)
    def __str__(self):
        return self.s
print(T('b') + 'c', 'a' + T('b'), T('x') + 1 + 2)
