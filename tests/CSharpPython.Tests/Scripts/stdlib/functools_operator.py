import operator
from functools import reduce
import functools

print(reduce(lambda a, b: a + b, [1, 2, 3, 4]), reduce(operator.mul, range(1, 6)), reduce(operator.add, [], 0), reduce(operator.add, ['a', 'b'], 'z'), reduce(max, [3, 9, 2]))
print(reduce(lambda a, b: a * 10 + b, [1, 2, 3], 5), reduce(operator.add, [[1], [2, 3]]), reduce(lambda acc, x: acc | {x}, [1, 2, 2], set()), reduce(lambda a, b: a, [7]))
for bad in (lambda: reduce(operator.add, []), lambda: reduce(1, [1, 2]), lambda: reduce(operator.add, 5), lambda: reduce(operator.add), lambda: reduce(operator.add, [1, 'a'])):
    try:
        bad()
    except TypeError as e:
        print('TypeError')
print(operator.add(2, 3), operator.sub(2, 3), operator.mul(2, 3), operator.truediv(7, 2), operator.floordiv(7, 2), operator.mod(7, 2), operator.pow(2, 5), operator.neg(4), operator.pos(-4), operator.invert(5))
print(operator.and_(6, 3), operator.or_(6, 3), operator.xor(6, 3), operator.lshift(1, 4), operator.rshift(64, 3), operator.concat([1], [2]), operator.concat('a', 'b'))
print(operator.eq(1, 1.0), operator.ne(1, 2), operator.lt(1, 2), operator.le(2, 2), operator.gt(1, 2), operator.ge(2, 2), operator.not_(0), operator.truth([]), operator.is_(None, None), operator.is_not(1, 2))
print(operator.contains([1, 2], 2), operator.contains('abc', 'd'), operator.getitem([5, 6, 7], -1), operator.getitem({'a': 1}, 'a'), operator.getitem('abc', 1), sorted([3, 1, 2], key=operator.neg))
print(list(map(operator.add, [1, 2], [10, 20])), list(map(operator.mul, 'ab', [2, 3])), sorted([(1, 'b'), (2, 'a')], key=lambda t: operator.getitem(t, 1)))
total = reduce(lambda acc, x: acc + x * x, range(5), 0)
print(total, functools.reduce(operator.mul, [1, 2, 3, 4]))
