class Money:
    def __init__(self, cents): self.cents = cents
    def __format__(self, spec):
        if spec == '':
            return 'M' + str(self.cents)
        if spec == 'd':
            return str(self.cents // 100) + '.' + str(self.cents % 100).zfill(2)
        return spec + ':' + str(self.cents)
m = Money(1234)
print(format(m), format(m, 'd'), format(m, 'xyz'))
print(f'{m}|{m:d}|{m:abc}')
print('{} {:d}'.format(m, m))
