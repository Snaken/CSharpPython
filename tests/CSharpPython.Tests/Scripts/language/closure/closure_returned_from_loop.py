def f():
    fs = []
    for i in range(3):
        fs.append(lambda i=i: i + 1)
    return fs
print(sum([f()[0](), f()[1](), f()[2]()]))
