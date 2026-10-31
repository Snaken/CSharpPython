class N:
    def __init__(self):
        self.me = self
n = N()
print(n.me is n, n.me.me.me is n)
