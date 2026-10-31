# indexing, slicing, ranges and membership

print([1, 2, 3][1:])

print([1, 2, 3][:2])

print([1, 2, 3][::-1])

print([1, 2, 3][::2])

print([1, 2, 3][5:])

print([1, 2, 3][-2:])

print([1, 2, 3][1:2])

print([1, 2, 3][:-1])

print([1, 2, 3][::-2])

print([1, 2, 3][2:0:-1])

print([1, 2, 3][-1:-4:-1])

print([1, 2, 3][100:200])

print([1,2,3][True])

print([1, 2, 3][0:0])

print([1,2,3,4,5][1:4:2])

print([1,2,3,4,5][::-3])

print([1,2,3,4,5][-2::-2])

print([1,2,3,4,5][3:-6:-1])

print((1, 2, 3)[1])

print((1, 2, 3)[1:])

print((1, 2, 3)[::-1])

print((1,)[0])

print(((1, 2), 3)[0][1])

print(range(5)[2])

print(range(5)[-1])

print(range(10)[2:5])

print(list(range(10)[::3]))

print(range(0, 10, 2)[3])

print(range(10)[::-1])

print(range(5))

print(range(1, 5))

print(range(1, 10, 2))

print(range(10, 0, -1))

print(list(range(10)[2:8:2]))

print(range(5)[1:3])

print(range(0))

print(2 in range(5))

print(5 in range(5))

print(-1 in range(5))

print(4 in range(0, 10, 2))

print(3 in range(0, 10, 2))

print(2.0 in range(5))

print('a' in range(3))

print(6 in range(10, 0, -2))

print(7 in range(10, 0, -2))

print(3 in [1, 2, 3])

print(3 in (1, 2))

print('a' in {'a': 1})

print(1 in {1.0: 2})

print(None in [None])

print([1] in [[1]])

print((1, 2) in [(1, 2)])

print(1.0 in [1])

print(True in [1])

print('ab' in ['ab'])

print('a' in ('a', 'b'))

print([1, 2] + [3])
