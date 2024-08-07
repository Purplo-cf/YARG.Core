//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using NUnit.Framework;
using YARG.Core.Chart;

#nullable enable

namespace YARG.Core.UnitTests.Chart;

public class NoteTrackerTests
{
    private List<GuitarNote> _notes = MakeNotes();

    private static List<GuitarNote> MakeNotes()
    {
        var notes = new List<GuitarNote>();

        for (uint i = 0; i < 20; i++)
        {
            var fret = FiveFretGuitarFret.Green;
            var type = GuitarNoteType.Strum;
            var guitarFlags = GuitarNoteFlags.None;
            var baseFlags = NoteFlags.None;

            var note = new GuitarNote(fret, type, guitarFlags, baseFlags, 0.5 * i, 0, 480 * i, 0);

            for (int n = 0; n < (i % 5); n++)
            {
                var child = new GuitarNote(++fret, type, guitarFlags, baseFlags,
                    note.Time, note.TimeLength, note.Tick, note.TickLength);
                note.AddChildNote(child);
            }

            notes.Add(note);
        }

        return notes;
    }


    [Test]
    public void Update_Tick()
    {
        void TestUpdate(uint increment)
        {
            var tracker = new NoteTickTracker<GuitarNote>(_notes);

            int expectedIndex = -1;
            for (uint tick = 0; tick < _notes[^1].Tick; tick += increment)
            {
                // Updates must occur when reaching or exceeding the tick of a new note
                bool updateExpected = false;
                while (expectedIndex + 1 < _notes.Count && _notes[expectedIndex + 1].Tick <= tick)
                {
                    expectedIndex++;
                    updateExpected = true;
                }

                bool updated = tracker.Update(tick);
                Assert.Multiple(() =>
                {
                    Assert.That(updated, Is.EqualTo(updateExpected));

                    Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                    Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

                    Assert.That(tracker.Current, Is.EqualTo(_notes[expectedIndex]));
                    Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[expectedIndex]));
                    Assert.That(tracker.CurrentChild, Is.Null);
                });

                // No more updates should occur until the next note once this update has run
                updated = tracker.Update(tick);
                Assert.Multiple(() =>
                {
                    Assert.That(updated, Is.False);

                    Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                    Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

                    Assert.That(tracker.Current, Is.EqualTo(_notes[expectedIndex]));
                    Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[expectedIndex]));
                    Assert.That(tracker.CurrentChild, Is.Null);
                });
            }
        }

        TestUpdate(100);
        TestUpdate(100 * 10);
    }

    [Test]
    public void UpdateOnce_Tick()
    {
        void TestUpdate(uint increment)
        {
            var tracker = new NoteTickTracker<GuitarNote>(_notes);

            int expectedIndex = -1;
            int expectedChildIndex = -1;
            for (uint tick = 0; tick < _notes[^1].Tick; tick += increment)
            {
                bool updated;

                // Updates must occur when reaching or exceeding the tick of a new note
                while (expectedIndex + 1 < _notes.Count && _notes[expectedIndex + 1].Tick <= tick)
                {
                    expectedIndex++;
                    expectedChildIndex = -1;

                    updated = tracker.UpdateOnce(tick, out var parent);
                    Assert.Multiple(() =>
                    {
                        Assert.That(updated, Is.True);

                        Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                        Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

                        Assert.That(tracker.Current, Is.EqualTo(_notes[expectedIndex]));
                        Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[expectedIndex]));
                        Assert.That(tracker.CurrentChild, Is.Null);

                        Assert.That(parent, Is.EqualTo(tracker.Current));
                        Assert.That(parent, Is.EqualTo(tracker.CurrentParent));
                    });

                    // An update must also occur for each child note
                    while (expectedChildIndex + 1 < parent!.ChildNotes.Count)
                    {
                        expectedChildIndex++;

                        updated = tracker.UpdateOnce(tick, out var child);
                        Assert.Multiple(() =>
                        {
                            Assert.That(updated, Is.True);

                            Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                            Assert.That(tracker.CurrentChildIndex, Is.EqualTo(expectedChildIndex));

                            Assert.That(tracker.Current, Is.EqualTo(_notes[expectedIndex].ChildNotes[expectedChildIndex]));
                            Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[expectedIndex]));
                            Assert.That(tracker.CurrentChild, Is.EqualTo(_notes[expectedIndex].ChildNotes[expectedChildIndex]));

                            Assert.That(child, Is.EqualTo(tracker.Current));
                            Assert.That(parent, Is.EqualTo(tracker.CurrentParent));
                            Assert.That(child, Is.EqualTo(tracker.CurrentChild));
                        });
                    }
                }

                // No more updates should occur until the next note once all available updates are run
                updated = tracker.UpdateOnce(tick, out var current);
                Assert.Multiple(() =>
                {
                    Assert.That(updated, Is.False);

                    Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                    Assert.That(tracker.CurrentChildIndex, Is.EqualTo(expectedChildIndex));

                    var expectedParent = expectedIndex >= 0 ? _notes[expectedIndex] : null;
                    var expectedChild = expectedChildIndex >= 0 ? expectedParent!.ChildNotes[expectedChildIndex] : null;
                    var expectedCurrent = expectedChild ?? expectedParent;

                    Assert.That(tracker.Current, Is.EqualTo(expectedCurrent));
                    Assert.That(tracker.CurrentParent, Is.EqualTo(expectedParent));
                    Assert.That(tracker.CurrentChild, Is.EqualTo(expectedChild));

                    Assert.That(current, Is.EqualTo(tracker.Current));
                });
            }
        }

        TestUpdate(100);
        TestUpdate(100 * 10);
    }

    [Test]
    public void Reset_Tick()
    {
        var tracker = new NoteTickTracker<GuitarNote>(_notes);

        // Update to end to ensure proper reset functionality
        bool updated = tracker.Update(_notes[2].Tick);
        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.True);

            Assert.That(tracker.CurrentParentIndex, Is.EqualTo(2));
            Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

            Assert.That(tracker.Current, Is.EqualTo(_notes[2]));
            Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[2]));
            Assert.That(tracker.CurrentChild, Is.Null);
        });

        // Resetting fully nullifies the current note
        tracker.Reset();
        Assert.Multiple(() =>
        {
            Assert.That(tracker.CurrentParentIndex, Is.EqualTo(-1));
            Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

            Assert.That(tracker.Current, Is.Null);
            Assert.That(tracker.CurrentParent, Is.Null);
            Assert.That(tracker.CurrentChild, Is.Null);
        });

        // Resetting to the tick of an note should result in that note being current
        for (int i = 0; i < _notes.Count; i++)
        {
            tracker.ResetToTick(_notes[i].Tick);
            Assert.Multiple(() =>
            {
                Assert.That(tracker.CurrentParentIndex, Is.EqualTo(i));
                Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

                Assert.That(tracker.Current, Is.EqualTo(_notes[i]));
                Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[i]));
                Assert.That(tracker.CurrentChild, Is.Null);
            });
        }
    }

    [Test]
    public void Update_Time()
    {
        void TestUpdate(double increment)
        {
            var tracker = new NoteTimeTracker<GuitarNote>(_notes);

            int expectedIndex = -1;
            for (double time = 0; time < _notes[^1].Time; time += increment)
            {
                // Updates must occur when reaching or exceeding the time of a new note
                bool updateExpected = false;
                while (expectedIndex + 1 < _notes.Count && _notes[expectedIndex + 1].Time <= time)
                {
                    expectedIndex++;
                    updateExpected = true;
                }

                bool updated = tracker.Update(time);
                Assert.Multiple(() =>
                {
                    Assert.That(updated, Is.EqualTo(updateExpected));

                    Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                    Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

                    Assert.That(tracker.Current, Is.EqualTo(_notes[expectedIndex]));
                    Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[expectedIndex]));
                    Assert.That(tracker.CurrentChild, Is.Null);
                });

                // No more updates should occur until the next note once this update has run
                updated = tracker.Update(time);
                Assert.Multiple(() =>
                {
                    Assert.That(updated, Is.False);

                    Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                    Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

                    Assert.That(tracker.Current, Is.EqualTo(_notes[expectedIndex]));
                    Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[expectedIndex]));
                    Assert.That(tracker.CurrentChild, Is.Null);
                });
            }
        }

        TestUpdate(0.1);
        TestUpdate(0.1 * 10);
    }

    [Test]
    public void UpdateOnce_Time()
    {
        void TestUpdate(double increment)
        {
            var tracker = new NoteTimeTracker<GuitarNote>(_notes);

            int expectedIndex = -1;
            int expectedChildIndex = -1;
            for (double time = 0; time < _notes[^1].Time; time += increment)
            {
                bool updated;

                // Updates must occur when reaching or exceeding the time of a new note
                while (expectedIndex + 1 < _notes.Count && _notes[expectedIndex + 1].Time <= time)
                {
                    expectedIndex++;
                    expectedChildIndex = -1;

                    updated = tracker.UpdateOnce(time, out var parent);
                    Assert.Multiple(() =>
                    {
                        Assert.That(updated, Is.True);

                        Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                        Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

                        Assert.That(tracker.Current, Is.EqualTo(_notes[expectedIndex]));
                        Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[expectedIndex]));
                        Assert.That(tracker.CurrentChild, Is.Null);

                        Assert.That(parent, Is.EqualTo(tracker.Current));
                        Assert.That(parent, Is.EqualTo(tracker.CurrentParent));
                    });

                    // An update must also occur for each child note
                    while (expectedChildIndex + 1 < parent!.ChildNotes.Count)
                    {
                        expectedChildIndex++;

                        updated = tracker.UpdateOnce(time, out var child);
                        Assert.Multiple(() =>
                        {
                            Assert.That(updated, Is.True);

                            Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                            Assert.That(tracker.CurrentChildIndex, Is.EqualTo(expectedChildIndex));

                            Assert.That(tracker.Current, Is.EqualTo(_notes[expectedIndex].ChildNotes[expectedChildIndex]));
                            Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[expectedIndex]));
                            Assert.That(tracker.CurrentChild, Is.EqualTo(_notes[expectedIndex].ChildNotes[expectedChildIndex]));

                            Assert.That(child, Is.EqualTo(tracker.Current));
                            Assert.That(parent, Is.EqualTo(tracker.CurrentParent));
                            Assert.That(child, Is.EqualTo(tracker.CurrentChild));
                        });
                    }
                }

                // No more updates should occur until the next note once all available updates are run
                updated = tracker.UpdateOnce(time, out var current);
                Assert.Multiple(() =>
                {
                    Assert.That(updated, Is.False);

                    Assert.That(tracker.CurrentParentIndex, Is.EqualTo(expectedIndex));
                    Assert.That(tracker.CurrentChildIndex, Is.EqualTo(expectedChildIndex));

                    var expectedParent = expectedIndex >= 0 ? _notes[expectedIndex] : null;
                    var expectedChild = expectedChildIndex >= 0 ? expectedParent!.ChildNotes[expectedChildIndex] : null;
                    var expectedCurrent = expectedChild ?? expectedParent;

                    Assert.That(tracker.Current, Is.EqualTo(expectedCurrent));
                    Assert.That(tracker.CurrentParent, Is.EqualTo(expectedParent));
                    Assert.That(tracker.CurrentChild, Is.EqualTo(expectedChild));

                    Assert.That(current, Is.EqualTo(tracker.Current));
                });
            }
        }

        TestUpdate(0.1);
        TestUpdate(0.1 * 10);
    }

    [Test]
    public void Reset_Time()
    {
        var tracker = new NoteTimeTracker<GuitarNote>(_notes);

        // Update to end to ensure proper reset functionality
        bool updated = tracker.Update(_notes[2].Time);
        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.True);

            Assert.That(tracker.CurrentParentIndex, Is.EqualTo(2));
            Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

            Assert.That(tracker.Current, Is.EqualTo(_notes[2]));
            Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[2]));
            Assert.That(tracker.CurrentChild, Is.Null);
        });

        // Resetting fully nullifies the current note
        tracker.Reset();
        Assert.Multiple(() =>
        {
            Assert.That(tracker.CurrentParentIndex, Is.EqualTo(-1));
            Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

            Assert.That(tracker.Current, Is.Null);
            Assert.That(tracker.CurrentParent, Is.Null);
            Assert.That(tracker.CurrentChild, Is.Null);
        });

        // Resetting to the time of an note should result in that note being current
        for (int i = 0; i < _notes.Count; i++)
        {
            tracker.ResetToTime(_notes[i].Time);
            Assert.Multiple(() =>
            {
                Assert.That(tracker.CurrentParentIndex, Is.EqualTo(i));
                Assert.That(tracker.CurrentChildIndex, Is.EqualTo(-1));

                Assert.That(tracker.Current, Is.EqualTo(_notes[i]));
                Assert.That(tracker.CurrentParent, Is.EqualTo(_notes[i]));
                Assert.That(tracker.CurrentChild, Is.Null);
            });
        }
    }
}