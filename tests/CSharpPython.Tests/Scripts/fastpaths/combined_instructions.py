# Runs of instructions that the VM combines: a local with a constant, a comparison with a jump, arithmetic on a local.
def compare_jump(n):
    out = []
    if n < 3: out.append('lt')
    if n <= 3: out.append('le')
    if n > 3: out.append('gt')
    if n >= 3: out.append('ge')
    if n == 3: out.append('eq')
    if n != 3: out.append('ne')
    return out

for value in [2, 3, 4, 2.5, 3.0, 3.5, True, 'a' == 'a', -1, 10 ** 30]:
    print(value, compare_jump(value))

def compare_kinds(a):
    r = []
    for b in [1, 1.0, True, 'x', None, (1,), [1]]:
        try:
            r.append(a < b)
        except TypeError:
            r.append('TypeError')
    return r

print(compare_kinds(1), compare_kinds(0.5), compare_kinds('w'))

def arithmetic(n):
    return [n + 1, n - 1, n * 2, n % 7, n // 3, n + 0.5, n - 0.5, n * 0.5]

for value in [5, -5, 0, 2 ** 62, -2 ** 62, 2 ** 63 - 1, -2 ** 63, 7.5, -7.5, True]:
    print(value, arithmetic(value))

def overflow():
    big = 2 ** 63 - 1
    small = -2 ** 63
    return big + 1, small - 1, big * 2, small * -1, big - -1

print(overflow())

def zero_division(n):
    out = []
    for f in (lambda: n % 0, lambda: n // 0, lambda: n / 0, lambda: n % 0.0):
        try:
            f()
        except ZeroDivisionError as e:
            out.append(type(e).__name__)
    return out

print(zero_division(5))

def containers():
    text = 'ab'
    items = [1]
    text += 'c'
    items += [2, 3]
    pair = (1,)
    pair += (2,)
    text = text * 2
    items = items * 2
    return text, items, pair, text + 'd', items + [9], 'x' + text[:1]

print(containers())

def unbound(flag):
    try:
        if flag:
            value = 1
        return value + 1
    except UnboundLocalError as e:
        return type(e).__name__

print(unbound(True), unbound(False))

def modulo_signs():
    return [(a % b, a // b) for a in (7, -7, 0) for b in (3, -3)]

print(modulo_signs())

def loops():
    total = 0
    i = 0
    while i < 10:
        i += 1
        if i % 2 == 0:
            continue
        if i > 7:
            break
        total += i * 2
    count = 0
    for x in range(20):
        if x != 5 and x < 15:
            count += 1
    return total, i, count

print(loops())

def attributes():
    class P:
        def __init__(self):
            self.x = 3
            self.y = 4.5

    p = P()
    q = P()
    return p.x + q.x, p.y * 2, p.x < q.y, p.x == q.x

print(attributes())

def jump_into_the_middle(n):
    result = 0
    for k in range(n):
        if k % 3 == 0 and k > 3:
            result += k - 1
        else:
            result -= 1
        result = result + 1 if k < 5 else result
    return result

print(jump_into_the_middle(12))
