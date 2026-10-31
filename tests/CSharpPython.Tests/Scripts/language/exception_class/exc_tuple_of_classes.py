class A(Exception):
    pass
class B(Exception):
    pass
for cls in (A, B, KeyError):
    try:
        raise cls('t')
    except (A, KeyError, B) as e:
        print(type(e).__name__)
