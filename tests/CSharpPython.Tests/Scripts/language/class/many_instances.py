class P:
    def __init__(self, i):
        self.i = i
ps = [P(i) for i in range(100)]
print(sum(p.i for p in ps), len(ps))
