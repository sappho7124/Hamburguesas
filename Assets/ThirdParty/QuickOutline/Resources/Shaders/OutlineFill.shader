Shader "Custom/Outline Fill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
    _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 10)) = 2
    
    // --- NEW HAND-DRAWN PROPERTIES ---
    _NoiseAmount("Noise Amount", Float) = 0.8
    _NoiseScale("Noise Scale", Float) = 15
    _FrameRate("Frame Rate", Float) = 24

    _StencilRef("Stencil Reference", Int) = 1 
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+110"
      "RenderType" = "Transparent"
      "DisableBatching" = "True"
    }

    Pass {
      Name "Fill"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref [_StencilRef] 
        Comp NotEqual
      }

      CGPROGRAM
      #include "UnityCG.cginc"

      #pragma vertex vert
      #pragma fragment frag

      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float3 smoothNormal : TEXCOORD3;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 position : SV_POSITION;
        fixed4 color : COLOR;
        UNITY_VERTEX_OUTPUT_STEREO
      };

      uniform fixed4 _OutlineColor;
      uniform float _OutlineWidth;

      // --- NEW UNIFORMS ---
      uniform float _NoiseAmount;
      uniform float _NoiseScale;
      uniform float _FrameRate;

      v2f vert(appdata input) {
        v2f output;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 normal = any(input.smoothNormal) ? input.smoothNormal : input.normal;
        
        // --- HAND DRAWN NOISE LOGIC ---
        // 1. Calculate the snapped time (Changes exactly 24 times a second)
        float snappedTime = floor(_Time.y * _FrameRate);

        // 2. Base the noise on the vertex position so it stays attached to the mesh shape
        float3 vpos = input.vertex.xyz * _NoiseScale;

        // 3. Create a smooth waving math function that looks like pencil variations
        float wave = sin(vpos.x + snappedTime) * cos(vpos.y + snappedTime * 0.73) * sin(vpos.z - snappedTime * 0.45);

        // 4. Modify the Outline Width by our wave pattern
        float finalWidth = _OutlineWidth + (wave * _NoiseAmount);
        
        // Ensure the width doesn't invert inside-out if the noise subtracts too much
        finalWidth = max(0.0, finalWidth);
        // ------------------------------

        float3 viewPosition = UnityObjectToViewPos(input.vertex);
        float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, normal));

        // Push the vertices out by the new fluctuating width
        output.position = UnityViewToClipPos(viewPosition + viewNormal * -viewPosition.z * finalWidth / 1000.0);
        output.color = _OutlineColor;

        return output;
      }

      fixed4 frag(v2f input) : SV_Target {
        return input.color;
      }
      ENDCG
    }
  }
}