class NoArgs:
    pass

class Two:
    __match_args__ = ('a', 'b')
    def __init__(self):
        self.a = 1
        self.b = 2

class Bad:
    __match_args__ = (1,)

for subject, pattern in [(NoArgs(), 'NoArgs(1)'), (Two(), 'Two(1, 2, 3)'), (5, 'int(1, 2)'), (5, 'x()'), (Bad(), 'Bad(1)'), ([1], 'list(1, 2)')]:
    x = 5
    try:
        exec_result = None
        if pattern == 'NoArgs(1)':
            match subject:
                case NoArgs(1):
                    pass
        elif pattern == 'Two(1, 2, 3)':
            match subject:
                case Two(1, 2, 3):
                    pass
        elif pattern == 'int(1, 2)':
            match subject:
                case int(1, 2):
                    pass
        elif pattern == 'x()':
            match subject:
                case x():
                    pass
        elif pattern == 'Bad(1)':
            match subject:
                case Bad(1):
                    pass
        else:
            match subject:
                case list(1, 2):
                    pass
    except TypeError:
        print('TypeError', pattern)

match Two():
    case Two(a=1, b=2) as both:
        print('both', both.a, both.b)
match Two():
    case Two(a=1, c=2):
        print('no')
    case Two(1, b=2):
        print('mixed')
match 5:
    case int(n):
        print('int', n)
match 'text':
    case str(s) | bytes(s):
        print('str', s)
match 2.5:
    case float(f) if f > 2:
        print('float', f)
match [1, 2, 3]:
    case list([a, *rest]):
        print('list', a, rest)
match {'a': 1}:
    case dict(d):
        print('dict', d)
match (1, 2):
    case tuple((x, y)):
        print('tuple', x, y)
match True:
    case bool(b):
        print('bool', b)
match 3:
    case str() | float():
        print('no')
    case int():
        print('int only')
try:
    raise ValueError('x')
except Exception as e:
    match e:
        case ValueError():
            print('value error')

def ordering():
    log = []
    def make(tag, value):
        log.append(tag)
        return value
    match make('subject', 3):
        case 1 if make('guard1', True):
            pass
        case 3 if make('guard3', False):
            pass
        case 3 if make('guard3b', True):
            log.append('body')
        case _:
            log.append('default')
    return log

print(ordering())

x = 'outer'
match [1, 2]:
    case [x, 3]:
        print('no')
    case [1, y]:
        print('x kept the binding of the failed case:', x, y)

class Registry:
    items = [10, 20]
    MODE = 'fast'
    match items:
        case [first, *_]:
            head = first
    kind = 'b' if MODE == 'fast' else 'c'

print(Registry.head, Registry.kind)

def gen(values):
    for v in values:
        match v:
            case int() if v > 1:
                yield 'big'
            case int():
                yield 'small'
            case _:
                return

print(list(gen([1, 2, 'stop', 3])))
