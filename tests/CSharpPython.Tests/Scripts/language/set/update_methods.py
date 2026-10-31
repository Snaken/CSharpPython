a = {1, 2, 3, 4}
a.intersection_update({2, 3, 9})
print(sorted(a))
a.difference_update([2], {7})
print(sorted(a))
a.symmetric_difference_update({3, 5})
print(sorted(a))
a.update()
a.intersection_update(a)
print(sorted(a))
b = {1, 2, 3}
c = b
b -= {1}
print(sorted(c), b is c)
b -= {9}
print(sorted(b))
