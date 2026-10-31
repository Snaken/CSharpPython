xs = list(range(10))
print(xs[slice(3)], xs[slice(2, 5)], xs[slice(1, 9, 3)], xs[slice(None, None, -1)], 'abcdef'[slice(1, None, 2)])
sl = slice(1, 4, 2)
print(sl.start, sl.stop, sl.step, slice(5).start, slice(5).stop, slice(5).step)
print(type(sl).__name__, isinstance(sl, slice), sl == slice(1, 4, 2), sl != slice(1, 4))
xs[slice(0, 2)] = [9]
print(xs)
