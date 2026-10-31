class Num:
    def __init__(self, v): self.v = v
    def __round__(self, ndigits=None):
        if ndigits is None:
            return int(self.v)
        return 'rounded to ' + str(ndigits)
n = Num(3.7)
print(round(n), round(n, 2), round(n, None), round(n, 0))
class No: pass
try:
    round(No())
except TypeError as e:
    print('TypeError')
