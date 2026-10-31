count = 0
class A:
    def hit(self):
        global count
        count += 1
a = A()
a.hit()
a.hit()
print(count)
