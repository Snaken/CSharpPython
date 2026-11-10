import random
random.seed(7)
big = [random.randint(-1000, 1000) for _ in range(500)]
print(sorted(big)[:10], sorted(big, reverse=True)[:10])
a = list(big); a.sort(); print(a == sorted(big), a[0], a[-1])
a.sort(reverse=True); print(a[0], a[-1])
print(sorted([3, 1, 2] * 10))
print(sorted(list(range(40, 0, -1)))[:5])
print(sorted([2**62, -2**62, 0, 5, -5] * 5)[:6])
# mixed kinds keep the general path
mixed = [3, True, 2, False, 1.5, 0] * 4
print(sorted(mixed))
print(sorted([2**70, 5, -3, 2**65] * 5)[:6])
# a key still works
print(sorted(big, key=lambda x: -x)[:5])
# sorting an empty and one-item list
e = []; e.sort(); print(e, sorted([9]))
# the original is untouched by sorted
src = [5, 4, 3] * 8
out = sorted(src)
print(src[:3], out[:3])
