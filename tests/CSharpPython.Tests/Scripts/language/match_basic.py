def describe(x):
    match x:
        case 0:
            return 'zero'
        case 1 | 2 | 3:
            return 'small'
        case -1:
            return 'minus one'
        case 3.5:
            return 'three and a half'
        case 'hello':
            return 'greeting'
        case 'a' | 'b':
            return 'letter'
        case None:
            return 'none'
        case True:
            return 'true'
        case False:
            return 'false'
        case [] | ():
            return 'empty sequence'
        case [1, 2]:
            return 'one two'
        case [first]:
            return 'single ' + str(first)
        case [first, *rest] if len(rest) > 2:
            return 'long ' + str(first) + str(rest)
        case (a, b):
            return 'pair %s %s' % (a, b)
        case {'kind': 'circle', 'r': r}:
            return 'circle %s' % r
        case {'kind': kind, **others}:
            return 'kind %s %s' % (kind, others)
        case {}:
            return 'empty dict'
        case str() as s:
            return 'string ' + s
        case int(n) if n > 100:
            return 'big ' + str(n)
        case float():
            return 'float'
        case _:
            return 'other'

for value in [0, 1, 3, -1, 3.5, 'hello', 'a', None, True, False, [], (), [1, 2], [7], [1, 2, 3, 4], (5, 6), {'kind': 'circle', 'r': 2}, {'kind': 'sq', 'w': 1, 'h': 2},
              {}, 'xyz', 500, 2.5, 50, {1: 2}, [1, 2, 3], set()]:
    print(repr(value), describe(value))

class Point:
    __match_args__ = ('x', 'y')
    def __init__(self, x, y):
        self.x = x
        self.y = y

class Circle:
    def __init__(self, center, radius):
        self.center = center
        self.radius = radius

class Color:
    RED = 1
    GREEN = 2

def shape(s):
    match s:
        case Point(0, 0):
            return 'origin'
        case Point(x, 0):
            return 'on x axis at %s' % x
        case Point(x=0, y=y):
            return 'on y axis at %s' % y
        case Point(x, y) if x == y:
            return 'diagonal %s' % x
        case Point(x, y):
            return 'point %s %s' % (x, y)
        case Circle(center=Point(x, y), radius=r):
            return 'circle at %s %s radius %s' % (x, y, r)
        case Circle(radius=r):
            return 'circle radius %s' % r
        case Color.RED:
            return 'red'
        case Color.GREEN | 99:
            return 'green or ninety nine'
        case _:
            return 'unknown'

for s in [Point(0, 0), Point(3, 0), Point(0, 4), Point(2, 2), Point(1, 2), Circle(Point(1, 2), 5), Circle(None, 3), 1, 2, 99, 'x', Point]:
    print(shape(s))

def nested(data):
    match data:
        case [[a, b], [c, d]]:
            return a + b + c + d
        case [('x', n), *tail]:
            return 'x%s+%d' % (n, len(tail))
        case {'user': {'name': name, 'tags': [first, *_]}}:
            return name + ':' + first
        case [*_, last]:
            return 'last ' + str(last)
        case _:
            return None

print(nested([[1, 2], [3, 4]]), nested([('x', 5), 1, 2]), nested({'user': {'name': 'bob', 'tags': ['a', 'b']}}), nested([9, 8, 7]), nested([]), nested(5))

def sequences(s):
    match s:
        case [a, b, *rest, z]:
            return (a, b, rest, z)
        case [a, *rest]:
            return (a, rest)
        case 'string':
            return 'matched str'
        case _:
            return 'no'

print(sequences([1, 2, 3, 4, 5]), sequences((1, 2, 3)), sequences([1]), sequences('abc'), sequences(range(3)), sequences('string'), sequences([]))

def tuples(p):
    match p:
        case 1, 2:
            return 'a'
        case 3, *r:
            return r
        case (4, 5) | (6, 7):
            return 'b'
        case [x, [y, z]] | [x, (y, z)]:
            return x + y + z
        case _:
            return '?'

print(tuples((1, 2)), tuples([3, 4, 5]), tuples((6, 7)), tuples([1, [2, 3]]), tuples([1, (2, 3)]), tuples(9))

command = 'go north'
match command.split():
    case ['go', direction]:
        print('going', direction)
    case ['quit']:
        print('bye')

match 3, 4:
    case x, y:
        print('tuple subject', x, y)

for item in [1, 'a', [1, 2], None]:
    match item:
        case int() | str() as simple:
            print('simple', simple)
            continue
        case [x, y]:
            print('pair', x + y)
        case _:
            print('skipped')
            break
    print('after', item)

total = 0
for n in range(6):
    match n % 3:
        case 0:
            total += 10
        case 1:
            total += 1
        case other:
            total += other * 100
print(total)

from collections import namedtuple
P = namedtuple('P', 'x y')
match P(1, 2):
    case P(a, b):
        print('namedtuple', a, b)
match P(1, 2):
    case (a, b):
        print('as tuple', a, b)
match [1, 2]:
    case P(a, b):
        print('wrong')
    case _:
        print('not a P')

match {'a': 1, 'b': 2, 'c': 3}:
    case {'a': 1, **rest}:
        print(rest)
match 5:
    case x if x > 10:
        print('big')
    case x:
        print('captured', x)

match = 'a soft keyword'
case = match
print(match, case)
def match_name(match):
    return match
print(match_name(3))
