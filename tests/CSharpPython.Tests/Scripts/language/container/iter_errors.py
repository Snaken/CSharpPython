class A:
    pass
class B:
    def __iter__(self):
        return 5
class C:
    def __iter__(self):
        return A()
for cls in (A, B, C):
    try:
        for x in cls():
            pass
    except TypeError as e:
        print(e)
