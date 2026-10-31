class A:
    tag = 'a'
class B(A):
    tag = 'b'
    def both(self):
        return self.tag, super().tag
print(B().both())
