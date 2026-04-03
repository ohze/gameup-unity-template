using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    public class CutoutMaskUI : Image
    {
        private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");

        // Override lại phương thức lấy Material để thay đổi cách render
        public override Material materialForRendering
        {
            get
            {
                var mat = new Material(base.materialForRendering);
                mat.SetInt(StencilComp, (int)CompareFunction.NotEqual);
                return mat;
            }
        }

    }
}