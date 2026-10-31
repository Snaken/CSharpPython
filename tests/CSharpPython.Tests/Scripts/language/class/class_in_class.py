class Outer:
    class Inner:
        def f(self):
            return 'in'
    def make(self):
        return Outer.Inner()
print(Outer().make().f(), Outer.Inner().f())
