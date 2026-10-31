class Rec:
    def __init__(self):
        self.log = []
    def __setitem__(self, k, v):
        self.log.append((k.start, k.stop, k.step, v))
r = Rec()
r[1:3] = 'x'
r[::2] = 5
print(r.log)
