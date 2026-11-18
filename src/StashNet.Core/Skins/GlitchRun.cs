using System;

namespace StashNet.Core.Skins
{
    /// <summary>The kinds of terminal screen glitch (owner, 2026-11-18: "a bit more of a random effect").</summary>
    public enum GlitchKind
    {
        /// <summary>Quick blinks: a broken frame, clean, broken...</summary>
        Flicker,
        /// <summary>Sideways shakes, now and then a broken frame.</summary>
        Jolt,
        /// <summary>The picture rolls vertically, like an old CRT losing its hold.</summary>
        Roll,
        /// <summary>Broken frames with jitter (the original glitch).</summary>
        Tear,
        /// <summary>The picture squashes vertically.</summary>
        Stretch,
    }

    /// <summary>What a screen shows during one 0.05 s step of a glitch.</summary>
    public readonly struct GlitchFrame
    {
        /// <summary>Frame value meaning "the TV's normal picture".</summary>
        public const int Rest = -1;

        public GlitchFrame(int frame, float offsetX, float offsetY, float scaleY)
        {
            Frame = frame;
            OffsetX = offsetX;
            OffsetY = offsetY;
            ScaleY = scaleY;
        }

        /// <summary><see cref="Rest"/>, or 1..damaged = one of the broken copies.</summary>
        public int Frame { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float ScaleY { get; }
    }

    /// <summary>
    /// One glitch, decided when it starts: a random kind, length and intensity (the paint.xml strength × 0.5-1.5), and
    /// sometimes a quick follow-up glitch so they come in bursts. <see cref="Step"/> gives each 0.05 s step's picture.
    /// Game-free; the TV's ScreenGlitcher applies it as a texture swap and offset/scale (no redraw).
    /// </summary>
    public sealed class GlitchRun
    {
        private const float JoltX = 0.07f;
        private const float StretchY = 0.25f;
        private readonly int damaged;
        private readonly float intensity;
        private readonly float rollSpeed;
        private readonly float stretch;

        public GlitchRun(Random rng, float maxLength, float strength, int damagedFrames)
        {
            damaged = Math.Max(0, damagedFrames);
            Kind = (GlitchKind)rng.Next(5);
            intensity = Math.Max(0f, Math.Min(1f, strength)) * (0.5f + (float)rng.NextDouble());
            float max = Math.Max(0.1f, maxLength);
            switch (Kind)
            {
                case GlitchKind.Flicker: Length = 0.08f + (float)rng.NextDouble() * 0.17f; break;      // short blinks
                case GlitchKind.Roll: Length = Math.Min(1f, max * (1f + (float)rng.NextDouble())); break; // a longer slide
                default: Length = ScreenGlitch.NextLength(rng, max); break;
            }
            rollSpeed = (0.6f + (float)rng.NextDouble() * 1.4f) * (rng.Next(2) == 0 ? 1f : -1f);
            stretch = 1f + Math.Min(StretchY, StretchY * intensity * (0.5f + (float)rng.NextDouble()));
            if (rng.NextDouble() < 1.0 / 3.0) FollowUpGap = 0.1f + (float)rng.NextDouble() * 0.4f;
        }

        public GlitchKind Kind { get; }

        /// <summary>Seconds this glitch lasts.</summary>
        public float Length { get; }

        /// <summary>Seconds until a quick second glitch (a burst), or null for a normal quiet gap afterwards.</summary>
        public float? FollowUpGap { get; }

        /// <summary>The picture for step <paramref name="index"/> (0, 1, 2... one per 0.05 s) of this glitch.</summary>
        public GlitchFrame Step(int index, Random rng)
        {
            switch (Kind)
            {
                case GlitchKind.Flicker:
                    return new GlitchFrame(index % 2 == 0 ? Broken(rng) : GlitchFrame.Rest, 0f, 0f, 1f);
                case GlitchKind.Jolt:
                    return new GlitchFrame(rng.Next(10) < 3 ? Broken(rng) : GlitchFrame.Rest, Shake(rng), 0f, 1f);
                case GlitchKind.Roll:
                    // Steady slide in one direction; the texture repeats, so the picture wraps round like a CRT roll.
                    return new GlitchFrame(GlitchFrame.Rest, 0f, rollSpeed * 0.05f * (index + 1), 1f);
                case GlitchKind.Stretch:
                    return new GlitchFrame(rng.Next(2) == 0 ? Broken(rng) : GlitchFrame.Rest, 0f, (stretch - 1f) * -0.5f, stretch);
                default: // Tear
                    int pick = rng.Next(3); // 0 offset only, 1 broken frame only, 2 both
                    float x = pick != 1 ? Shake(rng) : 0f;
                    return new GlitchFrame(pick != 0 ? Broken(rng) : GlitchFrame.Rest, x, 0f, 1f);
            }
        }

        private int Broken(Random rng) => damaged > 0 ? 1 + rng.Next(damaged) : GlitchFrame.Rest;

        private float Shake(Random rng) => ((float)rng.NextDouble() * 2f - 1f) * JoltX * intensity;
    }
}
