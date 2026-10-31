class Lock:
    def __init__(self):
        self.depth = 0
    def __enter__(self):
        self.depth += 1
        return self
    def __exit__(self, t, v, tb):
        self.depth -= 1
        return False
lock = Lock()
class Worker:
    def run(self, n):
        with lock:
            if n == 0:
                return lock.depth
            return self.run(n - 1)
print(Worker().run(3), lock.depth)
def g():
    with lock as l:
        return l is lock
print(g(), lock.depth)
