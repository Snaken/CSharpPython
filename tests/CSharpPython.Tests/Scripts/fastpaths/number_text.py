# str/repr/%d/f-string of plain numbers must match the general path
nums = [0, 1, -1, 7, -42, 2**62, -(2**62), 2**63 - 1, -(2**63), 2**63, 2**100, True, False]
for n in nums:
    print(str(n), repr(n), "%d" % n, "%i" % n, "%u" % n, f"{n}", "%s" % n, "%r" % n)
floats = [0.0, -0.0, 1.5, -2.25, 1e16, 1e-7, 123456789.125, float("inf"), float("-inf"), float("nan"), 0.1 + 0.2]
for f in floats:
    print(str(f), repr(f), f"{f}", "%s" % f, "%r" % f)
print("%d" % 3.9, "%d" % -3.9, "%5d|%-5d|%05d|%+d|% d|%.3d" % (42, 42, 42, 42, 42, 7))
print("%d items, %d total" % (3, 10))
print([1, -2, 3.5], (4, 5.5), {1: 2.5})
