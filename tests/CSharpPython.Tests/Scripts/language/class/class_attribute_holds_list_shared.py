class A:
    items = []
a = A()
b = A()
a.items.append(1)
print(b.items, A.items is a.items)
