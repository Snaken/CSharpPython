class Node:
    def __init__(self, v):
        self.v = v
        self.next = None
head = Node(1)
head.next = Node(2)
head.next.next = Node(3)
n = head
out = []
while n is not None:
    out.append(n.v)
    n = n.next
print(out)
