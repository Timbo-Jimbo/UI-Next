using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;
using UnityEngine.TestTools;

namespace TimboJimboTests.UINext
{
    public sealed class ReactiveTests
    {
        [TearDown]
        public void Flush() => UIScheduler.Flush(Phase.Draw);

        [Test]
        public void AComputationRunsAgainWhenSomethingItReadChanges()
        {
            var value = new Observable<int>(1);
            int runs = 0, seen = 0;
            var effect = new Effect(() => { runs++; seen = value.Value; });
            effect.Run();
            value.Value = 2;
            UIScheduler.Flush(Phase.Draw);
            Assert.AreEqual(2, runs);
            Assert.AreEqual(2, seen);
            effect.Dispose();
        }

        [Test]
        public void OnlyTheBranchTakenIsWatched()
        {
            var flag = new Observable<bool>(true);
            var a = new Observable<int>(1);
            var b = new Observable<int>(10);
            int runs = 0;
            var effect = new Effect(() => { runs++; _ = flag.Value ? a.Value : b.Value; });
            effect.Run();

            b.Value = 11;
            UIScheduler.Flush(Phase.Draw);
            Assert.AreEqual(1, runs, "b was not read, so changing it does nothing");

            flag.Value = false;
            UIScheduler.Flush(Phase.Draw);
            Assert.AreEqual(2, runs);

            a.Value = 2;
            UIScheduler.Flush(Phase.Draw);
            Assert.AreEqual(2, runs, "a is no longer read");

            b.Value = 12;
            UIScheduler.Flush(Phase.Draw);
            Assert.AreEqual(3, runs);
            effect.Dispose();
        }

        [Test]
        public void ManyWritesCostOneRun()
        {
            var value = new Observable<int>();
            int runs = 0;
            var effect = new Effect(() => { runs++; _ = value.Value; });
            effect.Run();
            for (int i = 1; i <= 50; i++)
                value.Value = i;
            UIScheduler.Flush(Phase.Draw);
            Assert.AreEqual(2, runs);
            effect.Dispose();
        }

        [Test]
        public void AnEarlierPhaseRunsFirst()
        {
            var source = new Observable<int>();
            var derived = new Observable<int>();
            var order = new List<string>();
            var layout = new Effect(() => { order.Add("layout"); _ = derived.Value; }, Phase.Layout);
            var model = new Effect(() => { order.Add("model"); derived.Value = source.Value * 2; }, Phase.Model);
            layout.Run();
            model.Run();
            order.Clear();

            source.Value = 5;
            layout.Invalidate();
            UIScheduler.Flush(Phase.Draw);
            Assert.AreEqual(new[] { "model", "layout" }, order.ToArray());
            Assert.AreEqual(10, derived.Peek);
            layout.Dispose();
            model.Dispose();
        }

        [Test]
        public void UntrackedReadsAreNotWatched()
        {
            var value = new Observable<int>();
            int runs = 0;
            var effect = new Effect(() => { runs++; Computation.Untracked(() => _ = value.Value); });
            effect.Run();
            value.Value = 1;
            UIScheduler.Flush(Phase.Draw);
            Assert.AreEqual(1, runs);
            effect.Dispose();
        }

        [Test]
        public void ADisposedComputationNeverRunsAgain()
        {
            var value = new Observable<int>();
            int runs = 0;
            var effect = new Effect(() => { runs++; _ = value.Value; });
            effect.Run();
            effect.Dispose();
            value.Value = 1;
            UIScheduler.Flush(Phase.Draw);
            Assert.AreEqual(1, runs);
            Assert.AreEqual(0, value.Readers.Count);
        }

        [Test]
        public void AComputationThatWritesWhatItReadsIsReported()
        {
            var value = new Observable<int>();
            var effect = new Effect(() => value.Value = value.Value + 1);
            effect.Run();
            LogAssert.Expect(LogType.Error, new Regex("without settling"));
            // Once it reads the value, a change sets it off, and each run invalidates it again.
            value.Value = 100;
            UIScheduler.Flush(Phase.Draw);
            effect.Dispose();
        }
    }
}
