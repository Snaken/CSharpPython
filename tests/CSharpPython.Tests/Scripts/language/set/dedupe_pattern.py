words = ['b', 'a', 'b', 'c', 'a']
unique = sorted(set(words))
print(unique, len(set(words)), set(words) == {'a', 'b', 'c'})
visited = set()
def walk(n):
    if n in visited or n > 5:
        return
    visited.add(n)
    walk(n + 1)
    walk(n * 2)
walk(1)
print(sorted(visited))
