class Base(Exception):
    pass
class Mid(Base):
    pass
class Leaf(Mid):
    pass
for cls in (Base, Mid, Leaf):
    try:
        raise Leaf('l')
    except cls as e:
        print(cls.__name__, type(e).__name__)
        break
print(issubclass(Leaf, Base), issubclass(Leaf, Exception), issubclass(Base, Leaf), issubclass(Leaf, ValueError))
