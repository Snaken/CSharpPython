class K:
    count = 0
    def bump(self):
        K.count += 1
a = K()
b = K()
a.bump()
b.bump()
print(K.count, a.count, b.count)
