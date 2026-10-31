class Acc:
    def __init__(self):
        self.log = []
    def __ior__(self, o):
        self.log.append('ior')
        return self
    def __iand__(self, o):
        self.log.append('iand')
        return self
    def __ixor__(self, o):
        self.log.append('ixor')
        return self
    def __ilshift__(self, o):
        self.log.append('ilshift')
        return self
    def __irshift__(self, o):
        self.log.append('irshift')
        return self
a = Acc()
b = a
a |= 1
a &= 1
a ^= 1
a <<= 1
a >>= 1
print(a.log, a is b)
class Plain:
    def __or__(self, o):
        return 'plain-or'
p = Plain()
p |= 1
print(p)
