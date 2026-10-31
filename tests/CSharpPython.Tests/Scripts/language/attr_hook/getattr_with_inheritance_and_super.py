class Base:
    def __getattr__(self, name):
        return 'base:' + name
class Child(Base):
    pass
class Over(Base):
    def __getattr__(self, name):
        if name == 'special':
            return 'over'
        return super().__getattr__(name)
print(Child().x, Over().special, Over().y)
