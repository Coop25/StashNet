using System;
using System.Linq;
using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    public class ScreenGlitchTests
    {
        private const int W = 256, H = 192;

        private static byte[] Clean()
        {
            var buf = new byte[W * H * 4];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = (y * W + x) * 4;
                    buf[i] = (byte)(x % 200); buf[i + 1] = (byte)(y % 250); buf[i + 2] = (byte)((x + y) % 255); buf[i + 3] = 255;
                }
            return buf;
        }

        [Fact]
        public void DamageIsDeterministicAndDifferent()
        {
            byte[] clean = Clean();
            byte[] copy = (byte[])clean.Clone();
            byte[] a = ScreenGlitch.Damage(clean, W, H, 5, 0.6f);
            byte[] b = ScreenGlitch.Damage(clean, W, H, 5, 0.6f);
            byte[] c = ScreenGlitch.Damage(clean, W, H, 6, 0.6f);
            Assert.Equal(a, b);
            Assert.False(a.SequenceEqual(c));
            Assert.False(a.SequenceEqual(clean));
            Assert.Equal(clean.Length, a.Length);
            Assert.Equal(clean, ScreenGlitch.Damage(clean, W, H, 5, 0f));
            Assert.Equal(copy, clean);
        }

        [Fact]
        public void StaticIsOpaqueAndVaries()
        {
            byte[] a = ScreenGlitch.Static(W, H, 1);
            byte[] b = ScreenGlitch.Static(W, H, 2);
            Assert.Equal(W * H * 4, a.Length);
            for (int i = 3; i < a.Length; i += 4) Assert.Equal(255, a[i]);
            Assert.False(a.SequenceEqual(b));
        }

        [Fact]
        public void GapAndLengthStayInRange()
        {
            var rng = new Random(3);
            for (int i = 0; i < 1000; i++)
            {
                float g = ScreenGlitch.NextGap(rng, 8f, 30f);
                Assert.InRange(g, 8f, 30f);
                float l = ScreenGlitch.NextLength(rng, 0.4f);
                Assert.InRange(l, 0.1f, 0.4f);
                Assert.Equal(0.1f, ScreenGlitch.NextLength(rng, 0.01f), 5);
            }
        }
    }
}
