class A:
    def __len__(self):
        return -1
class B:
    def __len__(self):
        return 'x'
class C:
    pass
for cls in (A, B, C):
    try:
        len(cls())
    except (TypeError, ValueError) as e:
        print(type(e).__name__, e)
