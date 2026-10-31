class Node:
    def __init__(self, v):
        self.v = v
        self.kids = []
    def __repr__(self):
        return 'Node(' + str(self.v) + ', ' + repr(self.kids) + ')'
a = Node(1)
a.kids.append(Node(2))
a.kids.append(Node(3))
print(a)
