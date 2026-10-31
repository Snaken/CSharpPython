class Base(Exception):
    def __init__(self, msg, level):
        super().__init__(msg)
        self.level = level
class Child(Base):
    pass
try:
    raise Child('c', 3)
except Base as e:
    print(e, e.level, type(e).__name__)
