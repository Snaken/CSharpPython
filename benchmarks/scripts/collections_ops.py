from collections import Counter, defaultdict, deque, namedtuple


def main():
    words = ['w%d' % (i % 500) for i in range(50000)]
    counts = Counter(words)
    assert len(counts) == 500 and counts['w0'] == 100
    groups = defaultdict(list)
    for i, w in enumerate(words):
        groups[w].append(i)
    assert len(groups['w1']) == 100
    queue = deque()
    total = 0
    for i in range(50000):
        queue.append(i)
        if len(queue) > 100:
            total += queue.popleft()
    assert total == sum(range(49900))
    Point = namedtuple('Point', 'x y')
    points = [Point(i, i * 2) for i in range(30000)]
    assert sum(p.x + p.y for p in points) == 3 * sum(range(30000))

main()
