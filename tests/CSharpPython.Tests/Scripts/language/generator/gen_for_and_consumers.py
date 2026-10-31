def squares(n):
    for i in range(n):
        yield i * i
for x in squares(4):
    print(x)
print(list(squares(5)), sum(squares(5)), max(squares(5)), min(squares(5)), sorted(squares(3), reverse=True))
print(tuple(squares(3)), set(squares(3)) == {0, 1, 4}, any(squares(3)), all(squares(3)))
print(list(enumerate(squares(3))), list(zip(squares(3), 'abc')))
print([a + 1 for a in squares(4)], {a: a for a in squares(3)})
a, b, c = squares(3)
print(a, b, c)
print(sum(x for x in squares(4)))
