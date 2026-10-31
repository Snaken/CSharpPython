class P:
    def __str__(self): return 'P!'
p = P()
print(f'{p}', format(p), format(p, ''), '{}'.format(p))
try:
    format(p, '>5')
except TypeError as e:
    print('TypeError')
class Bad:
    def __format__(self, spec): return 5
try:
    format(Bad())
except TypeError:
    print('TypeError')
