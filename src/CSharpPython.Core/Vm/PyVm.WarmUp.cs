using System;

namespace CSharpPython
{
    public sealed partial class PyVm
    {
        // Touches the paths a script commonly uses: functions and closures, classes, the collections, comprehensions,
        // generators, exceptions, string formatting, sorting with a key and the pause that saving a task needs.
        private const string WarmUpScript = @"
import math
import warm

class Racer:
    count = 0
    def __init__(self, name, x=0):
        self.name = name
        self.pos = (x, 0)
        self.seen = {}
        Racer.count += 1
    def __str__(self):
        return 'Racer(' + self.name + ')'
    def move(self, dx, dy):
        self.pos = (self.pos[0] + dx, self.pos[1] + dy)
        return self.pos

class Fast(Racer):
    def move(self, dx, dy):
        return super().move(dx * 2, dy * 2)

def make_counter():
    n = 0
    def bump(by=1):
        nonlocal n
        n += by
        return n
    return bump

def gen(limit):
    for i in range(limit):
        yield i, i * i

def distance(a, b):
    return abs(a[0] - b[0]) + abs(a[1] - b[1])

racers = [Racer('a'), Fast('b', 2)]
bump = make_counter()
seen = set()
cells = {}
frontier = [(0, 0)]
total = 0.0
for step in range(4):
    nxt = []
    for x, y in frontier:
        for d in sorted(range(4), key=lambda k: -k):
            c = (x + d, y + 1)
            if c not in seen:
                seen.add(c)
                cells[c] = distance(c, (0, 0))
                nxt.append(c)
    frontier = nxt[:3]
    for r in racers:
        r.move(1, 1)
    total += math.sqrt(bump(2)) + len(cells)
    warm.pause()

squares = [i * i for i in range(5) if i % 2 == 0]
lookup = {k: v for k, v in gen(4)}
unique = {len(str(v)) for v in squares}
pairs = list(zip(squares, enumerate(squares)))
text = ', '.join('%s=%d' % (k, v) for k, v in sorted(lookup.items()))
text = f'{text} {total:.2f} {racers[0]!s} {min(squares)} {max(squares)} {sum(squares)}'.upper().split(',')[0][1:4]
try:
    {}['missing']
except KeyError as e:
    text = repr(e)
finally:
    bump()
try:
    int('x')
except ValueError:
    pass
i = 0
while i < 3:
    i += 1
    if i == 2:
        continue
a, *rest = [1, 2, 3]
print(text, a, rest, pairs, unique, str(racers[1]), isinstance(racers[1], Racer))
";

        /// <summary>
        /// Runs a short script on a private VM so that the .NET runtime compiles the interpreter's code now, not the
        /// first time a game runs a script. Call it once while the game loads (before the first race, say). The
        /// first compile and the first <see cref="PyTask.Update"/> in a process cost about 30 ms between them (and the first <see cref="PyTask.Save"/> and <see cref="PyVm.Restore"/> another 9 ms) where code
        /// is compiled as it is first used (the Unity editor, Mono and desktop .NET), and nothing changes after a call
        /// to this method, which takes about 50 ms the first time and under 1 ms after that. It has no effect on a VM, a module or a script of
        /// yours, never throws, and may be called any number of times, from any thread. Under IL2CPP the code is
        /// compiled ahead of time and there is little to gain.
        /// </summary>
        /// <remarks>It also saves and restores a task, so the first <see cref="PyTask.Save"/> is warm too. The block editor has <c>BlockEditor.WarmUp</c> for its own code.</remarks>
        public static void WarmUp()
        {
            try
            {
                var vm = new PyVm(new VmOptions { EnableSnapshots = true });
                vm.RegisterModule("warm", m => m.Native("pause", (ctx, args) => { ctx.Wait(1); return Value.None; }, 0, 0));
                var script = vm.Compile(WarmUpScript, "<warm-up>");
                var task = vm.Start(script);
                for (int i = 0; i < 2; i++) task.Update();
                var copy = vm.Restore(script, task.Save());
                for (int i = 0; i < 100 && copy.Update() == TaskState.Running; i++) { }
                for (int i = 0; i < 1000 && (task.State == TaskState.Running || task.State == TaskState.Waiting); i++) task.Update();
            }
            catch (Exception)
            {
                // A warm-up is only a speed-up; a failure leaves the first real script to do the work.
            }
        }
    }
}
