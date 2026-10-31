class V:
    pass
for f in (lambda: V() < V(), lambda: V() >= 1, lambda: 1 <= V()):
    try:
        f()
    except TypeError as e:
        print(e)
