from collections import deque

def t(f):
    try:
        r = f()
        print(type(r).__name__, r)
    except Exception as e:
        print(type(e).__name__)

d = deque(maxlen=3)
print(d, d.maxlen)
for i in range(6):
    d.append(i)
    print(d, end=' ')
print()
for i in range(3):
    d.appendleft(i * 10)
    print(d, end=' ')
print()
print(deque(range(10), maxlen=4), deque(range(10), 4), deque([1, 2], maxlen=5), deque('abcdef', maxlen=2))
print(deque(range(5), maxlen=0), deque(maxlen=0).maxlen)
z = deque(maxlen=0)
z.append(1)
z.appendleft(2)
z.extend([1, 2])
print(z, len(z))
e = deque([1, 2, 3], maxlen=3)
e.extend([4, 5])
print(e)
e.extendleft([7, 8])
print(e)
e.rotate(1)
print(e, e.copy(), e.copy().maxlen, (e + deque([9])), (e + deque([9])).maxlen)
e += [100]
print(e)
t(lambda: e.insert(0, 1))
f = deque([1, 2], maxlen=3)
f.insert(1, 'a')
print(f)
t(lambda: f.insert(1, 'b'))
t(lambda: deque(maxlen=-1))
t(lambda: deque(maxlen='a'))
t(lambda: deque(maxlen=1.5))
t(lambda: deque([1], maxlen=None))
t(lambda: deque([1], None).maxlen)
t(lambda: setattr(f, 'maxlen', 5))
print(f.maxlen, deque(maxlen=2) == deque(maxlen=3), deque([1], maxlen=2) == deque([1]))

# a bounded deque as a sliding window
window = deque(maxlen=3)
averages = []
for value in [3, 5, 7, 11, 13, 17]:
    window.append(value)
    averages.append(sum(window) / len(window))
print(averages)

# a queue
queue = deque([('a', 1)])
order = []
while queue:
    name, depth = queue.popleft()
    order.append(name)
    if depth < 3:
        queue.append((name + 'l', depth + 1))
        queue.append((name + 'r', depth + 1))
print(order)
