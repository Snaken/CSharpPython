def lcg(seed, count, scale):
    out = []
    x = seed
    for _ in range(count):
        x = (x * 6364136223846793005 + 1442695040888963407) % (1 << 64)
        out.append((x >> 11) % scale - scale // 2)
    return out

def check(label, items):
    expected = sorted(items)
    a = list(items)
    a.sort()
    b = list(items)
    b.sort(reverse=True)
    ok = all(a[i] <= a[i + 1] for i in range(len(a) - 1))
    rev = all(b[i] >= b[i + 1] for i in range(len(b) - 1))
    ends = (a[0], a[-1], b[0], b[-1]) if a else ()
    print(label, len(a), ok, rev, a == expected, sum(a) == sum(items), ends)

for size in [0, 1, 2, 127, 128, 129, 255, 1000, 5000]:
    check("random%d" % size, lcg(size + 1, size, 2000000))

check("small values", lcg(7, 3000, 20))
check("big values", lcg(8, 3000, 1 << 62))
check("equal", [5] * 500)
check("sorted", list(range(-300, 300)))
check("reversed", list(range(300, -300, -1)))
check("extremes", [-(1 << 63), (1 << 63) - 1, 0, -1, 1, (1 << 62), -(1 << 62)] * 30)
check("one high byte differs", [i * (1 << 56) for i in range(-100, 100)])
check("only the sign differs", [-1, 0] * 200)

data = lcg(99, 2000, 1000)
sorted_once = sorted(data)
print(sorted_once[:10], sorted_once[-10:])
print(sorted(data, reverse=True)[:10])
print(sum(sorted_once[i] * (i + 1) for i in range(len(sorted_once))))
mixed = lcg(5, 200, 100) + [2.5]
mixed.sort()
print(mixed[:3], mixed[-3:])
