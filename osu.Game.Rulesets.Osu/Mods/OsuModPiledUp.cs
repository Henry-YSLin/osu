// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModPiledUp : ModWithVisibilityAdjustment, IApplicableToDrawableRuleset<OsuHitObject>
    {
        public override string Name => "Piled Up";

        public override string Description => "Where is the next circle?";

        public override double ScoreMultiplier => 1;

        public override string Acronym => "PU";

        /*
         * How this implementation works:
         *
         * - All hit objects in a map are grouped into batches of `batch_size`.
         * - HOs in the same batch are shown at the same time, which is when most HOs in the last batch expire,
         *   or at (StartTime - TimePreempt) of the first HO for the first batch.
         *       * This is done by adjusting the TimePreempt of each HO in ApplyToBeatmap.
         * - Each batch (except the first one) is also rendered to a framebuffer, which is shown on screen before
         *   that batch of HOs actually becomes visible.
         *       * This is done by creating a dummy DHO for each HO in the batch, then adding them to a BufferedContainer.
         * - Transforms are applied to the BufferedContainers so that they are hidden right as the corresponding
         *   batch of HOs are shown.
         *
         * As a result, there are no more than `batch_size + min_alive_count` number of HOs on screen at any time, but all future
         *   HOs still remain visible via framebuffers.
         */

        /// <summary>
        /// The number of hit objects to be grouped together in a framebuffer.
        /// </summary>
        private const int batch_size = 50;

        /// <summary>
        /// When there are this number of actual hit objects on screen, the next batch of hit objects will be shown.
        /// </summary>
        private const int min_alive_count = 10;

        /// <summary>
        /// Size of the buffer relative to the playfield. Anything outside the buffer boundary will be clipped.
        /// </summary>
        private const float buffer_size_multiplier = 1.2f;

        private const double buffer_fade_out = 100;
        private const double object_fade_in = 0;

        private readonly List<List<OsuHitObject>> batches = new List<List<OsuHitObject>> { new List<OsuHitObject>() };

        protected override void ApplyIncreasedVisibilityState(DrawableHitObject drawableObject, ArmedState state)
        {
        }

        protected override void ApplyNormalVisibilityState(DrawableHitObject drawableObject, ArmedState state)
            => applyNormalVisibilityState(drawableObject, state);

        private static void applyNormalVisibilityState(DrawableHitObject drawableObject, ArmedState state)
        {
            if (!(drawableObject is DrawableOsuHitObject))
                return;

            // Hide approach circle
            if (drawableObject is DrawableHitCircle circle)
            {
                circle.ApproachCircle.Hide();
            }

            // Disable slider snaking
            if (drawableObject is DrawableSlider slider)
            {
                SnakingSliderBody? sliderBody = slider.SliderBody;

                if (sliderBody == null)
                    return;

                sliderBody.SnakingIn.UnbindAll();
                sliderBody.SnakingOut.UnbindAll();
                sliderBody.SnakingIn.Value = false;
                sliderBody.SnakingOut.Value = false;
            }
        }

        public override void ApplyToBeatmap(IBeatmap beatmap)
        {
            OsuBeatmap osuBeatmap = (OsuBeatmap)beatmap;

            // Group hit objects into batches of batch_size
            foreach (var hitObject in osuBeatmap.HitObjects)
            {
                if (batches.Last().Count == batch_size)
                    batches.Add(new List<OsuHitObject>());
                batches.Last().Add(hitObject);
            }

            // Make hit objects appear in batches
            // When a new batch of hit objects appear, the corresponding buffered container will be hidden,
            // creating the illusion that the objects in the buffered container are "coming alive".
            for (int i = batches.Count - 1; i >= 1; i--)
            {
                synchronizeBatch(batches[i], batches[i - 1][batch_size - min_alive_count]);
            }

            synchronizeBatch(batches.First(), batches.First().First());

            base.ApplyToBeatmap(beatmap);
        }

        /// <summary>
        /// Make a batch of hit objects appear at the same time as the target hit object.
        /// </summary>
        /// <param name="batch">The batch of hit objects.</param>
        /// <param name="target">The target.</param>
        private void synchronizeBatch(IEnumerable<OsuHitObject> batch, OsuHitObject target)
        {
            foreach (var ho in batch)
            {
                ho.TimeFadeIn = object_fade_in;
                ho.TimePreempt = ho.StartTime - target.StartTime + target.TimePreempt;

                synchronizeBatch(ho.NestedHitObjects.Cast<OsuHitObject>(), target);
            }
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            // Render future hit objects into buffered containers, then display those containers

            var adjustmentContainer = drawableRuleset.CreatePlayfieldAdjustmentContainer();
            double firstStartTime = batches.First().First().StartTime - batches.First().First().TimePreempt;

            for (int i = batches.Count - 1; i >= 1; i--)
            {
                var container = new BufferedHitObjectsContainer(batches[i], firstStartTime);

                adjustmentContainer.Add(container);
            }

            drawableRuleset.FrameStableComponents.Add(adjustmentContainer);
        }

        /// <summary>
        /// Create a <see cref="DrawableOsuHitObject"/> based on the provided <see cref="OsuHitObject"/>, applying effects specific to this mod in the process.
        /// </summary>
        /// <param name="hitObject">A hit object.</param>
        /// <returns>A drawable representation of the hit object, with additional effects applied.</returns>
        private static DrawableOsuHitObject? createDrawableRepresentation(OsuHitObject hitObject)
        {
            DrawableOsuHitObject drawable;

            switch (hitObject)
            {
                case HitCircle circle:
                    drawable = new DrawableHitCircle(circle);
                    break;

                case Slider slider:
                    drawable = new DrawableSlider(slider);
                    break;

                case Spinner spinner:
                    drawable = new DrawableSpinner(spinner);
                    break;

                default:
                    return null;
            }

            drawable.ApplyCustomUpdateState += applyNormalVisibilityState;

            return drawable;
        }

        /// <inheritdoc />
        /// <summary>
        /// Render a batch of hit objects to a framebuffer, so that these objects can be visible without being present.
        /// </summary>
        private sealed class BufferedHitObjectsContainer : CachedBufferContainer
        {
            private readonly List<OsuHitObject> batch;
            private readonly double firstObjectTime;

            /// <summary>
            /// Create a buffered container rendering a batch of hit objects.
            /// </summary>
            /// <param name="batch">The batch of hit objects to be rendered.</param>
            /// <param name="firstObjectTime">The time when the first hit object in the beatmap becomes visible. Used to fade in this container.</param>
            public BufferedHitObjectsContainer(List<OsuHitObject> batch, double firstObjectTime)
                : base(pixelSnapping: true)
            {
                this.batch = batch;
                this.firstObjectTime = firstObjectTime;

                Alpha = 0;
                RelativeSizeAxes = Axes.Both;
                Size = new Vector2(buffer_size_multiplier);
                Origin = Anchor.Centre;
                Anchor = Anchor.Centre;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // Seek to a time when the hit objects are visible
                var stopwatchClock = new StopwatchClock();
                stopwatchClock.Seek(batch.First().StartTime - batch.First().TimePreempt + object_fade_in);
                var clock = new FramedOffsetClock(stopwatchClock, false);

                var container = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(1 / buffer_size_multiplier),
                    Origin = Anchor.Centre,
                    Anchor = Anchor.Centre
                };

                for (int j = batch.Count - 1; j >= 0; j--)
                {
                    var drawable = createDrawableRepresentation(batch[j]);

                    if (drawable == null)
                        continue;

                    drawable.Clock = clock;
                    container.Add(drawable);
                }

                LoadComponentAsync(container, Add);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                // Fade in at the start of the map
                using (BeginAbsoluteSequence(firstObjectTime))
                    this.FadeInFromZero(object_fade_in);

                // Fade out when the corresponding batch of hit objects are displayed for real
                // Use fade out with duration to reduce flicker
                using (BeginAbsoluteSequence(batch.First().StartTime - batch.First().TimePreempt))
                    this.FadeOut(buffer_fade_out);
            }
        }
    }
}
