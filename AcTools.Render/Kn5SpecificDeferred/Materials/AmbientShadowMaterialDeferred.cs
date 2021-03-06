﻿using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Utils.Helpers;
using SlimDX;

namespace AcTools.Render.Kn5SpecificDeferred.Materials {
    public class AmbientShadowMaterialDeferred : IRenderableMaterial {
        private readonly string _filename;
        private EffectDeferredGObject _effect;

        private IRenderableTexture _txDiffuse;

        internal AmbientShadowMaterialDeferred(string filename) {
            _filename = filename;
        }

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDeferredGObject>();

            var texturesProvider = contextHolder.Get<TexturesProvider>();
            _txDiffuse = texturesProvider.GetTexture(_filename, contextHolder);
        }

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Deferred) return false;

            _effect.FxDiffuseMap.SetResource(_txDiffuse);
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPT;
            contextHolder.DeviceContext.OutputMerger.BlendState = contextHolder.TransparentBlendState;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.LessEqualReadOnlyDepthState;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechAmbientShadowDeferred.DrawAllPasses(contextHolder.DeviceContext, indices);
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
        }

        public bool IsBlending => false;

        public void Dispose() {
            DisposeHelper.Dispose(ref _txDiffuse);
        }
    }
}
