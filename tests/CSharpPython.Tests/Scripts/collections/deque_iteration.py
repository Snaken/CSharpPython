from collections import deque

def t(f):
    try:
        r = f()
        print(type(r).__name__, r)
    except Exception as e:
        print(type(e).__name__)

d = deque([1, 2, 3])
it = iter(d)
print(next(it))
d.rotate(1)
t(lambda: next(it))

d = deque([1, 2, 3])
def grow():
    for x in d:
        d.append(x)
t(grow)

d = deque([1, 2, 3])
seen = []
for x in d:
    seen.append(x)
print(seen)

d = deque(range(5))
it = iter(d)
print(list(it), list(it))
print(list(zip(d, reversed(d))))
print(list(enumerate(d, 1)))
d2 = deque(d)
d2.append(99)
print(d == deque(range(5)), d2 != d)

big = deque()
for i in range(1000):
    big.append(i)
    if i % 3 == 0:
        big.popleft()
print(len(big), big[0], big[-1], big[100], sum(big))
for i in range(500):
    big.appendleft(-i)
print(len(big), big[0], big[499], big[500], big[-1])
total = 0
while big:
    total += big.pop()
    if big:
        total -= big.popleft()
print(total, len(big))
