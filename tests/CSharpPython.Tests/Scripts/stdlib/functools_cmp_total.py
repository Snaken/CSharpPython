from functools import cmp_to_key, total_ordering
import itertools


def by_length_then_alpha(a, b):
    if len(a) != len(b):
        return len(a) - len(b)
    if a < b:
        return -1
    if a > b:
        return 1
    return 0


words = ["pear", "fig", "apple", "kiwi", "date", "banana", "cherry"]
print(sorted(words, key=cmp_to_key(by_length_then_alpha)))
print(sorted(words, key=cmp_to_key(by_length_then_alpha), reverse=True))
print(min(words, key=cmp_to_key(by_length_then_alpha)), max(words, key=cmp_to_key(by_length_then_alpha)))
xs = [3, 1, 2]
xs.sort(key=cmp_to_key(lambda a, b: b - a))
print(xs)
print(sorted([(1, 'b'), (1, 'a'), (0, 'z')], key=cmp_to_key(lambda a, b: (a[0] > b[0]) - (a[0] < b[0]))))

k = cmp_to_key(lambda a, b: a - b)
print(k(1) < k(2), k(2) < k(1), k(2) <= k(2), k(3) > k(2), k(2) >= k(3))
print(type(k).__name__)

# A stable sort keeps equal elements in order
pairs = [(2, 'a'), (1, 'b'), (2, 'c'), (1, 'd')]
print(sorted(pairs, key=cmp_to_key(lambda a, b: a[0] - b[0])))


@total_ordering
class Version:
    def __init__(self, major, minor):
        self.major = major
        self.minor = minor

    def __eq__(self, other):
        return (self.major, self.minor) == (other.major, other.minor)

    def __lt__(self, other):
        return (self.major, self.minor) < (other.major, other.minor)

    def __repr__(self):
        return "V" + str(self.major) + "." + str(self.minor)


a, b = Version(1, 2), Version(1, 3)
print(a < b, a <= b, a > b, a >= b, a == b, a != b)
print(b < a, b <= a, b > a, b >= a)
print(a <= Version(1, 2), a >= Version(1, 2), a > Version(1, 2))
print(sorted([Version(2, 0), Version(1, 5), Version(1, 1)]))
print(max(Version(2, 0), Version(1, 5)))


@total_ordering
class Rank:
    def __init__(self, n):
        self.n = n

    def __eq__(self, other):
        return self.n == other.n

    def __ge__(self, other):
        return self.n >= other.n


print(Rank(1) < Rank(2), Rank(2) < Rank(1), Rank(1) <= Rank(1), Rank(2) > Rank(1), Rank(1) > Rank(1), Rank(3) >= Rank(3))


@total_ordering
class Gt:
    def __init__(self, n):
        self.n = n

    def __eq__(self, other):
        return self.n == other.n

    def __gt__(self, other):
        return self.n > other.n


print(Gt(1) < Gt(2), Gt(2) < Gt(2), Gt(2) <= Gt(2), Gt(1) >= Gt(2), Gt(3) >= Gt(2))


@total_ordering
class Le:
    def __init__(self, n):
        self.n = n

    def __eq__(self, other):
        return self.n == other.n

    def __le__(self, other):
        return self.n <= other.n


print(Le(1) < Le(2), Le(2) < Le(2), Le(2) > Le(2), Le(3) > Le(2), Le(2) >= Le(3), Le(3) >= Le(3))


class Sub(Version):
    pass


print(Sub(1, 1) < Sub(1, 2), Sub(1, 3) >= Sub(1, 2))

try:
    @total_ordering
    class Nothing:
        pass
except ValueError as e:
    print(e)

# tee
first, second = itertools.tee(iter([1, 2, 3]))
print(list(first), list(second))
first, second = itertools.tee(x * 2 for x in range(4))
print(next(first), next(first), next(second), list(second), list(first))
trio = itertools.tee("ab", 3)
print(len(trio), [list(t) for t in trio])
print(itertools.tee([1, 2], 0))
c1, c2 = itertools.tee(itertools.count(5))
print(list(itertools.islice(c1, 3)), list(itertools.islice(c2, 5)), next(c1))
try:
    itertools.tee([1], -1)
except ValueError as e:
    print(e)
(only,) = itertools.tee([7], 1)
print(list(only), list(only))
