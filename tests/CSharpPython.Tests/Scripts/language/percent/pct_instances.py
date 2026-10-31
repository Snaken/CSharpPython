class S:
    def __str__(self): return 'S!'
    def __repr__(self): return 'R!'
print('%s %r %a' % (S(), S(), S()))
class I:
    def __index__(self): return 10
    def __int__(self): return 3
    def __float__(self): return 2.5
print('%d %x %f' % (I(), I(), I()))
class N:
    pass
try:
    print('%d' % N())
except TypeError as e:
    print(e)
