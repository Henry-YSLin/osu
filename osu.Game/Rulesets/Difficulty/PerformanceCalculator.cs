// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Audio.Mixing;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Difficulty
{
    public abstract class PerformanceCalculator
    {
        protected readonly DifficultyAttributes Attributes;

        protected readonly Ruleset Ruleset;
        protected readonly ScoreInfo Score;

        protected double TimeRate { get; private set; } = 1;

        protected PerformanceCalculator(Ruleset ruleset, DifficultyAttributes attributes, ScoreInfo score)
        {
            Ruleset = ruleset;
            Score = score;

            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));

            ApplyMods(score.Mods);
        }

        protected virtual void ApplyMods(Mod[] mods)
        {
            var track = new TrackVirtual(10000);
            var mixer = new AudioMixerVirtual();
            mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track, mixer));
            TimeRate = track.Rate;
        }

        public abstract PerformanceAttributes Calculate();
    }
}
