class T:
    def __init__(self, d):
        self.d = d
    def child(self):
        return T(self.d + 1)
print(T(0).child().child().d)
