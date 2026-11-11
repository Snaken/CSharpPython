def length(x):
    return len(x)

def twice(x):
    return len(x) * 2

print(length([1, 2, 3]), twice("ab"))

# a script defines its own len after the built-in was used from the same places
def len(x):
    return -1

print(length([1, 2, 3]), twice("ab"))

# deleting it brings the built-in back
del len
print(length([1, 2, 3]), twice("ab"))

# a global that is reassigned in a loop is read fresh each time
counter = 0
def bump():
    global counter
    counter += 1
    return counter

seen = []
for _ in range(4):
    seen.append(bump() + counter)
print(seen)

def read_counter():
    return counter

counter = 100
print(read_counter())
counter = 200
print(read_counter())

# a global that is deleted and defined again
value = 1
def read_value():
    return value

print(read_value())
del value
try:
    read_value()
except NameError:
    print("NameError")
value = 2
print(read_value())

# a missing name raises every time it is read
def missing():
    return nothing_here

for _ in range(2):
    try:
        missing()
    except NameError as e:
        print(e)

# a name defined later in the program, read before and after by the same function
def late():
    return later_name

try:
    late()
except NameError:
    print("not yet")
later_name = "now"
print(late())

# a built-in shadowed and restored in a loop
def use_max(a, b):
    return max(a, b)

out = []
for i in range(4):
    if i == 2:
        max = lambda a, b: "shadow"
    if i == 3:
        del max
    out.append(use_max(1, 5))
print(out)

# functions and classes called through globals while others are defined
def fib(n):
    return n if n < 2 else fib(n - 1) + fib(n - 2)

print(fib(15))
class K:
    pass
print(type(K()).__name__)
