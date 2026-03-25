Shader "Custom/Outline Mask" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
    // NEW: Allow the script to change the ID
    _StencilRef("Stencil Reference", Int) = 1 
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+100"
      "RenderType" = "Transparent"
    }

    Pass {
      Name "Mask"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      ColorMask 0

      Stencil {
        // UPDATED: Use the variable instead of hardcoded 1
        Ref [_StencilRef] 
        Pass Replace
      }
    }
  }
}