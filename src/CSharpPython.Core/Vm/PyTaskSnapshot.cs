using System;
using System.Collections.Generic;
using System.IO;
using CSharpPython.Compilation;

namespace CSharpPython
{
    public sealed partial class PyTask
    {
        private const ushort SnapshotVersion = 13;
        private static readonly byte[] SnapshotMagic = { (byte)'C', (byte)'S', (byte)'P', (byte)'Y' };

        // The deepest value stack a restored snapshot may claim, so that damaged data cannot ask for gigabytes.
        private const int MaxRestoredStack = 1 << 24;

        /// <summary>
        /// Writes the task's whole state (globals, call stack, pending waits and events, iterators in progress) to
        /// bytes that <see cref="PyVm.Restore"/> can turn back into an equivalent task, in this or a later session.
        /// Requires <see cref="VmOptions.EnableSnapshots"/>. Host objects are saved as the handles scripts hold, so
        /// the host must save and restore the objects themselves. Host functions the script can reach must be
        /// registered as built-ins or in modules, because they are saved by name.
        /// </summary>
        public byte[] Save()
        {
            if (!Vm.Options.EnableSnapshots)
                throw new SnapshotException("snapshots are disabled; set VmOptions.EnableSnapshots to true");

            var writer = new SnapshotWriter(Vm, _script);
            writer.Collect(_result);
            foreach (var kv in Globals) writer.Collect(kv.Value);
            foreach (var ev in _events) foreach (var a in ev.Args) writer.Collect(a);
            for (int i = 0; i < _sp; i++) writer.Collect(_stack[i]);
            for (int i = 0; i < _frameCount; i++)
            {
                writer.Collect(_frames[i].ExcInfo);
                if (_frames[i].IsConstructor) writer.Collect(_frames[i].Constructed);
                if (_frames[i].Owner != null) writer.Collect(Value.FromClass(_frames[i].Owner));
                if (_frames[i].Generator != null) writer.Collect(Value.FromIterator(_frames[i].Generator));
                writer.Collect(_frames[i].GenDefault);
                if (_frames[i].Closure != null)
                    foreach (var cell in _frames[i].Closure) writer.Collect(Value.FromCell(cell));
            }

            writer.Collect(_lastReturn);

            var stream = new MemoryStream();
            var w = new BinaryWriter(stream);
            w.Write(SnapshotMagic);
            w.Write(SnapshotVersion);
            w.Write(_script.Fingerprint);
            writer.WriteTable(w);

            w.Write((byte)_state);
            w.Write(_waitTicks);
            w.Write(_mainWait);
            w.Write(_inEvent);
            w.Write(_moduleDone);
            w.Write(_totalSteps);
            writer.WriteValue(w, _result);

            w.Write(_error != null);
            if (_error != null)
            {
                SnapshotIo.WriteString(w, _error.TypeName);
                SnapshotIo.WriteString(w, _error.Problem);
                SnapshotIo.WriteString(w, _error.SourceName);
                w.Write(_error.Trace.Count);
                foreach (var f in _error.Trace)
                {
                    SnapshotIo.WriteString(w, f.Function);
                    w.Write(f.Line);
                }
            }

            w.Write(Globals.Count);
            foreach (var kv in Globals)
            {
                SnapshotIo.WriteString(w, kv.Key);
                writer.WriteValue(w, kv.Value);
            }

            w.Write(_events.Count);
            foreach (var ev in _events)
            {
                SnapshotIo.WriteString(w, ev.Name);
                w.Write(ev.Args.Length);
                foreach (var a in ev.Args) writer.WriteValue(w, a);
            }

            w.Write(_sp);
            for (int i = 0; i < _sp; i++) writer.WriteValue(w, _stack[i]);

            w.Write(_frameCount);
            for (int i = 0; i < _frameCount; i++)
            {
                var f = _frames[i];
                int codeIndex = _script.IndexOf(f.Code);
                if (codeIndex < 0) throw new SnapshotException("a running function does not belong to this script");
                w.Write(codeIndex);
                w.Write(f.Pc);
                w.Write(f.Base);
                w.Write(f.CalleeIndex);
                w.Write(f.IsEvent);
                w.Write(f.HandlerCount);
                for (int h = 0; h < f.HandlerCount; h++)
                {
                    w.Write(f.Handlers[h].Address);
                    w.Write(f.Handlers[h].Sp);
                }
                writer.WriteValue(w, f.ExcInfo);
                int cells = f.Closure?.Length ?? 0;
                w.Write(cells);
                for (int c = 0; c < cells; c++) writer.WriteValue(w, Value.FromCell(f.Closure[c]));
                w.Write(f.IsConstructor);
                if (f.IsConstructor) writer.WriteValue(w, f.Constructed);
                writer.WriteValue(w, f.Owner == null ? Value.None : Value.FromClass(f.Owner));
                writer.WriteValue(w, f.Generator == null ? Value.None : Value.FromIterator(f.Generator));
                w.Write((byte)f.GenMode);
                writer.WriteValue(w, f.GenDefault);
            }
            writer.WriteValue(w, _lastReturn);

            w.Write(Random != null);
            Random?.Write(w);

            w.Flush();
            return stream.ToArray();
        }

        internal static PyTask RestoreTask(PyVm vm, Script script, byte[] data)
        {
            if (!vm.Options.EnableSnapshots)
                throw new SnapshotException("snapshots are disabled; set VmOptions.EnableSnapshots to true");
            if (script == null) throw new ArgumentNullException(nameof(script));
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                return ReadTask(vm, script, data);
            }
            catch (SnapshotException)
            {
                throw;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                throw new SnapshotException("the snapshot is damaged", e);
            }
        }

        private static PyTask ReadTask(PyVm vm, Script script, byte[] data)
        {
            var r = new BinaryReader(new MemoryStream(data, false));
            var magic = r.ReadBytes(4);
            if (magic.Length != 4 || magic[0] != SnapshotMagic[0] || magic[1] != SnapshotMagic[1] ||
                magic[2] != SnapshotMagic[2] || magic[3] != SnapshotMagic[3])
                throw new SnapshotException("this is not a CSharpPython snapshot");
            ushort version = r.ReadUInt16();
            if (version != SnapshotVersion)
                throw new SnapshotException("snapshot version " + version + " is not supported (expected " + SnapshotVersion + ")");
            if (r.ReadUInt64() != script.Fingerprint)
                throw new SnapshotException("the snapshot was taken from a different script");

            var reader = new SnapshotReader(vm, script, data);
            reader.ReadTable(r);

            var task = new PyTask(vm, script, true);
            byte state = r.ReadByte();
            if (state > (byte)TaskState.Error) throw new SnapshotException("the snapshot is damaged");
            task._state = (TaskState)state;
            task._waitTicks = r.ReadInt32();
            task._mainWait = r.ReadInt32();
            task._inEvent = r.ReadBoolean();
            task._moduleDone = r.ReadBoolean();
            task._totalSteps = r.ReadInt64();
            task._result = reader.ReadValue(r);

            if (r.ReadBoolean())
            {
                string type = SnapshotIo.ReadString(r);
                string problem = SnapshotIo.ReadString(r);
                string source = SnapshotIo.ReadString(r);
                int n = SnapshotIo.ReadCount(r, 8);
                var trace = new List<TraceFrame>(n);
                for (int i = 0; i < n; i++)
                {
                    string fn = SnapshotIo.ReadString(r);
                    trace.Add(new TraceFrame(fn, r.ReadInt32()));
                }
                task._error = new PyRuntimeError(type, problem, source, trace);
            }

            int globals = SnapshotIo.ReadCount(r, 5);
            for (int i = 0; i < globals; i++)
            {
                string name = SnapshotIo.ReadString(r);
                task.Globals[name] = reader.ReadValue(r);
            }

            int events = SnapshotIo.ReadCount(r, 8);
            for (int i = 0; i < events; i++)
            {
                string name = SnapshotIo.ReadString(r);
                int argc = SnapshotIo.ReadCount(r, 1);
                var args = new Value[argc];
                for (int a = 0; a < argc; a++) args[a] = reader.ReadValue(r);
                task._events.Enqueue(new PendingEvent { Name = name, Args = args });
            }

            int sp = r.ReadInt32();
            if (sp < 0 || sp > MaxRestoredStack || sp > r.BaseStream.Length - r.BaseStream.Position)
                throw new SnapshotException("the snapshot is damaged");
            task.EnsureStack(sp + 8);
            for (int i = 0; i < sp; i++) task._stack[i] = reader.ReadValue(r);
            task._sp = sp;

            int frames = SnapshotIo.ReadCount(r, 20);
            if (frames > vm.Options.MaxCallDepth + 2) throw new SnapshotException("the snapshot has more nested calls than MaxCallDepth allows");
            if (frames >= task._frames.Length) Array.Resize(ref task._frames, frames + 1);
            for (int i = 0; i < frames; i++)
            {
                int codeIndex = r.ReadInt32();
                if (codeIndex < 0 || codeIndex >= script.Codes.Length) throw new SnapshotException("the snapshot does not match this script");
                var code = script.Codes[codeIndex];
                var f = new Frame { Code = code };
                f.Pc = r.ReadInt32();
                f.Base = r.ReadInt32();
                f.CalleeIndex = r.ReadInt32();
                f.IsEvent = r.ReadBoolean();
                int handlers = SnapshotIo.ReadCount(r, 8);
                f.Handlers = new Handler[Math.Max(handlers, 4)];
                for (int h = 0; h < handlers; h++)
                {
                    f.Handlers[h].Address = r.ReadInt32();
                    f.Handlers[h].Sp = r.ReadInt32();
                    if (f.Handlers[h].Address < 0 || f.Handlers[h].Address > code.Instructions.Length || f.Handlers[h].Sp < 0 || f.Handlers[h].Sp > sp)
                        throw new SnapshotException("the snapshot is damaged");
                }
                f.HandlerCount = handlers;
                f.ExcInfo = reader.ReadValue(r);
                f.Closure = reader.ReadClosure(r, code);
                f.IsConstructor = r.ReadBoolean();
                if (f.IsConstructor)
                {
                    f.Constructed = reader.ReadValue(r);
                    if (f.Constructed.Kind != ValueKind.Instance) throw new SnapshotException("the snapshot is damaged");
                }
                Value owner = reader.ReadValue(r);
                if (owner.Kind == ValueKind.Class) f.Owner = (PyClass)owner.RawObject;
                else if (owner.Kind != ValueKind.None) throw new SnapshotException("the snapshot is damaged");
                Value generator = reader.ReadValue(r);
                if (generator.Kind == ValueKind.Iterator && generator.RawObject is PyGenerator running && running.Code == code
                    && running.State == GeneratorState.Running)
                    f.Generator = running;
                else if (generator.Kind != ValueKind.None) throw new SnapshotException("the snapshot is damaged");
                byte genMode = r.ReadByte();
                if (genMode > (byte)GenMode.Close || (genMode != 0) != (f.Generator != null)) throw new SnapshotException("the snapshot is damaged");
                f.GenMode = (GenMode)genMode;
                f.GenDefault = reader.ReadValue(r);
                if (f.Pc < 0 || f.Pc > code.Instructions.Length || f.Base < 0 || f.Base + code.NumLocals > sp ||
                    f.CalleeIndex < 0 || f.CalleeIndex > f.Base)
                    throw new SnapshotException("the snapshot is damaged");
                task._frames[i] = f;
            }
            task._frameCount = frames;
            task._lastReturn = reader.ReadValue(r);
            if (r.ReadBoolean()) task.Random = RandomState.Read(r);

            if (task._state != TaskState.Finished && task._state != TaskState.Error && frames == 0)
                throw new SnapshotException("the snapshot is damaged");
            if (r.BaseStream.Position != r.BaseStream.Length)
                throw new SnapshotException("the snapshot is damaged (unexpected trailing data)");
            return task;
        }

        /// <summary>
        /// Advances the task by <paramref name="ticks"/> game ticks as if <see cref="Update"/> had been called that
        /// many times, but skips quickly over stretches where the script is only waiting. Use it to catch a
        /// restored task up with the time that passed while the game was closed.
        /// </summary>
        public TaskState FastForward(long ticks)
        {
            while (ticks > 0)
            {
                if (_state == TaskState.Error) break;
                if (_state == TaskState.Finished && _events.Count == 0) break;
                if (_state == TaskState.Waiting && !_inEvent && _events.Count == 0 && _waitTicks > 1)
                {
                    // The last tick of a wait runs normally; the ones before it only count down.
                    long skip = Math.Min(ticks, _waitTicks - 1);
                    _waitTicks -= (int)skip;
                    ticks -= skip;
                    continue;
                }
                Update();
                ticks--;
            }
            return _state;
        }
    }
}
