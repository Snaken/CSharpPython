def f():
    xs = [3,1,2]
    def s():
        return sorted(xs)
    return s()
print(f())
