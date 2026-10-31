# comparisons, chained comparisons and logic operators

print(1 == 1.0)

print(1 == True)

print(0 == False)

print(1 == '1')

print(None == 0)

print(0 == None)

print(1 != 2)

print('a' != 'a')

print([1, 2] == [1, 2])

print([1, 2] == (1, 2))

print((1, 2) == (1, 2))

print({'a': 1} == {'a': 1})

print({'a': 1} == {'a': 2})

print({1: 2} == {1.0: 2})

print([] == [])

print([[1]] == [[1]])

print((1, [2]) == (1, [2]))

print(float('nan') == float('nan'))

print('a' == 'a')

print(1 < 2)

print(2 <= 2)

print(3 > 2)

print(2 >= 3)

print(1 < 2.5)

print(True < 2)

print('a' < 'b')

print('B' < 'a')

print([1, 2] < [1, 3])

print([1, 2] < [1, 2, 0])

print((1, 2) < (1, 3))

print(() < (1,))

print([] < [0])

print('' < 'a')

print((1, 'a') < (1, 'b'))

print([2] > [1, 5])

print(1 and 2)

print(0 and 2)

print(1 or 2)

print(0 or 2)

print(0 or 0)

print('' or None)

print([] or {})

print(not 0)

print(not 1)

print(not None)

print(not 'a')

print(not [])

print(-True)

print(+True)

print(-(1.5))

print(- -3)

print(not not 5)

print(1 if True else 2)

print(1 if False else 2)

print((1 if 0 else 2) + 1)

print(0xFF)

print(0b1010)

print(0o17)

print(1_000_000)

print(0XAB)

print(1E2)

print(1.e2 if False else 0)

print(.5 + .5)

print(5 .real if False else 5)

print(10 // 3 * 3 + 10 % 3)

print(3 * 'ab')

print('ab' * -1)

print([1] * 0)

print([1, 2] * 2)

print((1,) * 3)

print([[0]] * 2)

print(True * 'a')

print(0 * [1])

print('a' * True)

print(1 < 2 == True)

print(1 == 1 != 2)

print(1 < 2 > 1)

print(0 < 1 < 2 < 3 < 4)

print(5 > 3 > 4)

print(2 + 3 * 4 ** 2 // 5 % 7)

print(-2 ** 2)

print(2 ** 3 ** 2)

print(2 ** -1 ** 2 if False else 0)

print(-3 % 5)

print(- 3 % 5)

print(7 - 3 - 2)

print(2 * 3 / 4)

print(8 / 4 / 2)

print(100 // 7 // 2)

print(1 + 2 - 3 + 4)
