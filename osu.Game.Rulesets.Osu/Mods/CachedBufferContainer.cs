// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shaders;
using osuTK;
using osuTK.Graphics;
using osuTK.Graphics.ES30;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class CachedBufferContainer : Container, IBufferedContainer, IBufferedDrawable
    {
        public Vector2 BlurSigma
        {
            get;
            set;
        } = Vector2.Zero;

        private readonly CachedBufferContainerDrawNodeSharedData sharedData;

        /// <summary>
        /// Constructs an empty buffered container.
        /// </summary>
        /// <param name="formats">The render buffer formats attached to the frame buffers of this <see cref="CachedBufferContainer"/>.</param>
        /// <param name="pixelSnapping">
        /// Whether the frame buffer position should be snapped to the nearest pixel when blitting.
        /// This amounts to setting the texture filtering mode to "nearest".
        /// </param>
        public CachedBufferContainer(RenderbufferInternalFormat[]? formats = null, bool pixelSnapping = false)
        {
            sharedData = new CachedBufferContainerDrawNodeSharedData(formats, pixelSnapping);
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            RoundedTextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE_ROUNDED);
        }

        protected override DrawNode CreateDrawNode() => new CachedBufferContainerDrawNode(this, sharedData);

        protected override RectangleF ComputeChildMaskingBounds(RectangleF maskingBounds) => ScreenSpaceDrawQuad.AABBFloat; // Make sure children never get masked away

        // Children should not receive the true colour to avoid colour doubling when the frame-buffers are rendered to the back-buffer.
        public override DrawColourInfo DrawColourInfo
        {
            get
            {
                // Todo: This is incorrect.
                var blending = Blending;
                blending.ApplyDefaultToInherited();

                return new DrawColourInfo(Color4.White, blending);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (sharedData.BufferDrawn)
            {
                Clear();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            sharedData.Dispose();
        }

        private class CachedBufferContainerDrawNode : BufferedDrawNode, ICompositeDrawNode
        {
            private readonly CachedBufferContainerDrawNodeSharedData sharedData;
            protected new CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

            public CachedBufferContainerDrawNode(CachedBufferContainer source, CachedBufferContainerDrawNodeSharedData sharedData)
                : base(source, new CompositeDrawableDrawNode(source), sharedData)
            {
                this.sharedData = sharedData;
            }

            public List<DrawNode> Children
            {
                get => Child.Children;
                set => Child.Children = value;
            }

            protected override void PopulateContents()
            {
                base.PopulateContents();
                sharedData.BufferDrawn = true;
            }

            public bool AddChildDrawNodes => RequiresRedraw;
        }

        public class CachedBufferContainerDrawNodeSharedData : BufferedDrawNodeSharedData
        {
            public bool BufferDrawn { get; set; }

            public CachedBufferContainerDrawNodeSharedData(RenderbufferInternalFormat[]? formats, bool pixelSnapping)
                : base(0, formats, pixelSnapping)
            {
            }
        }

        public IShader? TextureShader { get; private set; }
        public IShader? RoundedTextureShader { get; private set; }

        public Color4 BackgroundColour => new Color4(0, 0, 0, 0);

        public DrawColourInfo? FrameBufferDrawColour => null;

        public Vector2 FrameBufferScale => Vector2.One;
    }
}
