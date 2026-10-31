a = {1, 2, 3}
b = {3, 4}
print(sorted(a | b), sorted(a & b), sorted(a ^ b), sorted(a - b))
print(sorted(a | a), sorted(a & set()), sorted(a ^ a), sorted(set() | set()))
c = a
c |= {9}
print(sorted(a), c is a)
c &= {1, 9, 10}
print(sorted(a), c is a)
c ^= {9, 7}
print(sorted(a), c is a)
d = {1, 2}
e = d
d |= d
d &= d
print(sorted(d), e is d)
d ^= d
print(sorted(e))
def attempt(fn):
    try:
        fn()
    except TypeError as e:
        print('TypeError', e)
attempt(lambda: {1} | [2])
attempt(lambda: [2] | {1})
attempt(lambda: {1} & 3)
attempt(lambda: {1} ^ (1,))
s = {1}
try:
    s |= [2]
except TypeError as e:
    print('TypeError', e)
print(sorted(s))
