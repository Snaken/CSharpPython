classes = []
for i in range(3):
    class K:
        v = i
    classes.append(K)
print([k.v for k in classes], classes[0] is classes[1])
