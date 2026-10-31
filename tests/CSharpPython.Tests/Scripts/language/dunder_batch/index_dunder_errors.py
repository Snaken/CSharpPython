class Bad:
    def __index__(self): return 'x'
class NoIdx: pass
for thing in (Bad(), NoIdx()):
    try:
        [1, 2][thing]
    except TypeError:
        print('TypeError')
try:
    hex(NoIdx())
except TypeError:
    print('TypeError')
try:
    range(Bad())
except TypeError:
    print('TypeError')
