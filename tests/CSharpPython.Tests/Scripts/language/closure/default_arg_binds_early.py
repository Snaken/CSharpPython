fs = []
for i in range(3):
    fs.append(lambda i=i: i)
print(fs[0](), fs[1](), fs[2]())
