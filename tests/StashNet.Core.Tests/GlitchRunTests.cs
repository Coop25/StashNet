using System;
using System.Collections.Generic;
using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    public class GlitchRunTests
    {
        const int Damaged = 4;

        [Fact]
        public void EveryKindShowsUp()
        {
            var rng = new Random(7);
            var seen = new HashSet<GlitchKind>();
            for (int i = 0; i < 500; i++) seen.Add(new GlitchRun(rng, 0.4f, 0.6f, Damaged).Kind);
            foreach (GlitchKind k in Enum.GetValues(typeof(GlitchKind))) Assert.Contains(k, seen);
        }

        [Fact]
        public void SameSeedSameGlitch()
        {
            var a = new GlitchRun(new Random(3), 0.4f, 0.6f, Damaged);
            var b = new GlitchRun(new Random(3), 0.4f, 0.6f, Damaged);
            Assert.Equal(a.Kind, b.Kind);
            Assert.Equal(a.Length, b.Length);
        }

        [Fact]
        public void StepsStayInBounds()
        {
            var rng = new Random(11);
            for (int i = 0; i < 300; i++)
            {
                var run = new GlitchRun(rng, 0.4f, 1f, Damaged);
                Assert.InRange(run.Length, 0.05f, 1f);
                for (int s = 0; s < 20; s++)
                {
                    GlitchFrame f = run.Step(s, rng);
                    Assert.True(f.Frame == GlitchFrame.Rest || (f.Frame >= 1 && f.Frame <= Damaged), $"{run.Kind} frame {f.Frame}");
                    Assert.InRange(f.OffsetX, -0.1f, 0.1f);
                    Assert.InRange(f.ScaleY, 1f, 1.4f);
                }
            }
        }

        [Fact]
        public void FlickerBlinksBetweenBrokenAndClean()
        {
            GlitchRun run = Find(GlitchKind.Flicker);
            var rng = new Random(1);
            Assert.NotEqual(GlitchFrame.Rest, run.Step(0, rng).Frame);
            Assert.Equal(GlitchFrame.Rest, run.Step(1, rng).Frame);
            Assert.NotEqual(GlitchFrame.Rest, run.Step(2, rng).Frame);
        }

        [Fact]
        public void RollMovesOneWay()
        {
            GlitchRun run = Find(GlitchKind.Roll);
            var rng = new Random(1);
            float a = run.Step(1, rng).OffsetY, b = run.Step(2, rng).OffsetY, c = run.Step(3, rng).OffsetY;
            Assert.True(Math.Abs(b) > Math.Abs(a) && Math.Abs(c) > Math.Abs(b));
        }

        [Fact]
        public void StretchSquashesThePicture()
        {
            GlitchRun run = Find(GlitchKind.Stretch);
            Assert.True(run.Step(0, new Random(1)).ScaleY > 1f);
        }

        [Fact]
        public void SomeGlitchesComeInBursts()
        {
            var rng = new Random(5);
            int bursts = 0;
            for (int i = 0; i < 1000; i++)
            {
                var run = new GlitchRun(rng, 0.4f, 0.6f, Damaged);
                if (run.FollowUpGap is float gap)
                {
                    bursts++;
                    Assert.InRange(gap, 0.1f, 0.5f);
                }
            }
            Assert.InRange(bursts, 200, 450); // about a third
        }

        [Fact]
        public void NoDamagedFramesMeansRestOnly()
        {
            var rng = new Random(2);
            for (int i = 0; i < 100; i++)
            {
                var run = new GlitchRun(rng, 0.4f, 0.6f, 0);
                Assert.Equal(GlitchFrame.Rest, run.Step(0, rng).Frame);
            }
        }

        static GlitchRun Find(GlitchKind kind)
        {
            var rng = new Random(9);
            for (int i = 0; i < 1000; i++)
            {
                var run = new GlitchRun(rng, 0.4f, 0.6f, Damaged);
                if (run.Kind == kind) return run;
            }
            throw new Exception("kind never picked: " + kind);
        }
    }
}
