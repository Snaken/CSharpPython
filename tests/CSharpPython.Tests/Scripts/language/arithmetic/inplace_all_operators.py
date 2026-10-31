class N:
    def __init__(self):
        self.log = []
    def __iadd__(self, o): self.log.append('+'); return self
    def __isub__(self, o): self.log.append('-'); return self
    def __imul__(self, o): self.log.append('*'); return self
    def __itruediv__(self, o): self.log.append('/'); return self
    def __ifloordiv__(self, o): self.log.append('//'); return self
    def __imod__(self, o): self.log.append('%'); return self
    def __ipow__(self, o): self.log.append('**'); return self
n = N()
n += 1
n -= 1
n *= 1
n /= 1
n //= 1
n %= 1
n **= 1
print(n.log)
