Shader "Modified Toon/Toon 3D as 2D (URP)"{
    Properties{
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (Off = Both Sides)", Float) = 2
        [HideInInspector] _Color ("Script Target Color", Color) = (1,1,1,1) 
        
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        [MainTexture] _MainTex ("Main Texture", 2D) = "white" {}

        //Three Colors
        _1st_ShadeColor ("1st Shade Color", Color) = (0.5,0.5,0.5,1)
        [NoScaleOffset] _1st_ShadeMap ("1st Shade Map", 2D) = "white" {}
        [Toggle(_)] _Use_BaseAs1st ("Use BaseMap as 1st_ShadeMap", Integer ) = 0
        _2nd_ShadeColor ("2nd Shade Color", Color) = (0.1,0.1,0.1,1)
        [NoScaleOffset] _2nd_ShadeMap ("2nd Shade Map", 2D) = "white" {}
        [Toggle(_)] _Use_1stAs2nd ("Use 1st ShadeMap as 2nd ShadeMap", Integer ) = 0
        
        //Start and Feather
        _BaseTo1st_ShadeStart ("Base to 1st Shade Start", Range(0, 1)) = 0.5
        _BaseTo1st_ShadeFeather ("Base to 1st Shade Feather", Range(0, 1)) = 0.1
        _1stTo2nd_ShadeStart ("1st to 2nd Shade Start", Range(0, 1)) = 0.25
        _1stTo2nd_ShadeFeather ("1st to 2nd Shade Feather", Range(0, 1)) = 0.1
        
        _2DLightStrength ("2D Light Strength", Range(0,1)) = 1

        [NoScaleOffset] _MaskTex("Mask", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 1)) = 1
        
        [HideInInspector] _White("Tint", Color) = (1,1,1,1) 
        
        //Directional Light
        _DirectionalLight_Use ("Use Directional Light", Integer) = 0
        _DirectionalLight_Direction ("Directional Light Direction", Vector) = (0,-1,0,0)
        _DirectionalLight_Color("Directional Light Color", Color) = (1,1,1,1)
        _DirectionalLight_Intensity ("Directional Light Intensity", float) = 0.5
        _DirectionalLight_DiffuseStrength ("Directional Light: Diffuse Strength", Range(0,1)) = 0.5

        _DirectionalLight_ViewPosition ("Directional Light: View Position", Vector) = (0,0,1,0)
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)
        [NoScaleOffset] _HighlightTex ("HighColor Map", 2D) = "white" {}
        _DirectionalLight_HighlightMode ("Directional Light: Highlight Mode", Integer) = 0 //0: Hard, 1: Soft
        _DirectionalLight_HighlightStrength ("Directional Light: Highlight Strength", Range(0,1)) = 0.5
        _DirectionalLight_HighlightSize ("Directional Light: Highlight Size", Range(0,1)) = 0.3
        
        //Outline
        _OutlineMode("Outline Mode", Integer) = 0
        _OutlineWidth ("Outline Width", Float ) = 5
        [NoScaleOffset] _OutlineWidthMap ("Outline Width Map", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.1,0.1,0.1,1)
        [NoScaleOffset] _OutlineTex ("Outline Tex", 2D) = "white" {}
        _Outline_BaseColorBlend ("Blend Base Color to Outline", Range(0,1) ) = 0.5
        _Outline_LightColorBlend ("Blend Light Color to Outline", Range(0,1) ) = 0.5
        _OutlineOffsetZ ("Outline Z Offset", Float) = 0.75
        _OutlineNear ("Outline Near", Float ) = 0.5
        _OutlineFar ("Outline Far", Float ) = 100
        _Outline_UseNormalMap ("Outline: Use Outline Normal Map", Integer ) = 0
        [NoScaleOffset] _Outline_NormalMap ("Outline Normal Map", 2D) = "bump" {}
        [HideInInspector] _ToonMaterialVersion ("Toon Material Version", Integer ) = 0
    }

    // =================================================================================================
    // MACRO DE CBUFFER COMPARTIDO (Para que el SRP Batcher acepte el Tiling en tiempo real en todos los pases)
    // =================================================================================================
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _BaseColor;
            float _BumpScale;
            float4 _1st_ShadeColor;
            int _Use_BaseAs1st;
            float4 _2nd_ShadeColor;
            int _Use_1stAs2nd;
            float _BaseTo1st_ShadeStart;
            float _BaseTo1st_ShadeFeather;
            float _1stTo2nd_ShadeStart;
            float _1stTo2nd_ShadeFeather;
            float _2DLightStrength;
            int _DirectionalLight_Use;
            float3 _DirectionalLight_Direction;
            float4 _DirectionalLight_Color;
            float _DirectionalLight_Intensity;
            float _DirectionalLight_DiffuseStrength;
            float3 _DirectionalLight_ViewPosition;
            float4 _HighlightColor;
            int _DirectionalLight_HighlightMode;
            float _DirectionalLight_HighlightStrength;
            float _DirectionalLight_HighlightSize;
            float _OutlineOffsetZ;
            float _OutlineWidth; 
            float _OutlineNear; 
            float _OutlineFar;
            float4 _OutlineColor;
            float _Outline_BaseColorBlend;
            float _Outline_LightColorBlend;
            int _Outline_UseNormalMap;

            // ESTA ES LA VARIABLE QUE TU SCRIPT MODIFICA. AHORA ESTÁ DISPONIBLE GLOBALMENTE.
            float4 _MainTex_ST;
        CBUFFER_END
    ENDHLSL

    SubShader{
        PackageRequirements {
             "com.unity.render-pipelines.universal": "17.3.0" 
        }
        Tags{
            "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull [_Cull] 
        ZWrite On

        Stencil{
            Ref 128 
            Comp always
            Pass replace
        }

        // ====================================================================================
        // PASS UNIVERSAL 2D (EL PASE PRINCIPAL)
        // ====================================================================================
        Pass{
            Tags{ "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY

            struct Attributes {
                float3 positionOS   : POSITION; 
                float2 uv           : TEXCOORD0;
                float3 normal       : NORMAL;  
                float4 tangent      : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;  
                half2 lightingUV    : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 tangentWS   : TEXCOORD3;
                float3 positionWS  : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _White;
            
            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);          SAMPLER(sampler_MaskTex);
            TEXTURE2D(_NormalMap);        SAMPLER(sampler_NormalMap);
            TEXTURE2D(_1st_ShadeMap);
            TEXTURE2D(_2nd_ShadeMap);
            TEXTURE2D(_HighlightTex);     SAMPLER(sampler_HighlightTex);

            #include "Packages/com.unity.toonshader/Runtime/Shaders/URP/ObjectTransform.hlsl"
            #include "Packages/com.unity.toonshader/Runtime/Shaders/URP/ShapeLight2D.hlsl"
            #include "Packages/com.unity.toonshader/Runtime/Shaders/UTSLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl" 
            
            Varyings ToonVertex(Attributes input) {
                Varyings o = (Varyings) 0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(input.positionOS);
                const float3 normalWS = TransformObjectToWorldDir(input.normal);
    
                // ¡AQUI SE APLICA EL TILING Y OFFSET DE TU SCRIPT PARA TODO EL SHADER!
                o.uv = TRANSFORM_TEX(input.uv, _MainTex); 

                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.normalWS = normalWS;

                const float3 tangentWS = normalize( mul( GetObjectToWorldMatrix(), float4( input.tangent.xyz, 0 ) ).xyz); 
                o.tangentWS = float4(tangentWS, input.tangent.w);
                o.positionWS = TransformObjectToWorld(input.positionOS);
                
                return o;
            }

            half4 CombinedShapeLightAndToon(ShapeLightResult shapeLightResult, SurfaceData2D surfaceData,
                in float2 uv, in float3 tangentWS, in float3 bitangentWS, in float3 normalWS, in float3 positionWS)
            {
                const half alpha = surfaceData.alpha;
                float3x3 tangentTransform = float3x3( tangentWS, bitangentWS, normalWS);
                const float3 normalTS = surfaceData.normalTS;
                float3 perturbedNormalWS = normalize(mul( normalTS, tangentTransform )); 

                half4 light2dMod = shapeLightResult.mod; 
                half4 light2dAdd = shapeLightResult.add; 

                const float light2dIntensity = max(light2dMod.r * light2dAdd.r, max(light2dMod.g + light2dAdd.g, light2dMod.b + light2dAdd.b));
                
                const half4 mainTex = half4(surfaceData.albedo, alpha);
                const float3 baseAlbedo = _BaseColor.rgb * _Color.rgb * mainTex.rgb;

                // Todas las sombras e iluminaciones usan la UV compensada por tu script
                const float4 firstShadeTex = lerp(SAMPLE_TEXTURE2D(_1st_ShadeMap, sampler_MainTex, uv), mainTex, _Use_BaseAs1st);
                const float3 firstShadeAlbedo = _1st_ShadeColor.rgb * firstShadeTex.rgb; 

                const float4 secondShadeTex = lerp(SAMPLE_TEXTURE2D(_2nd_ShadeMap, sampler_MainTex, uv), firstShadeTex, _Use_1stAs2nd);
                const float3 secondShadeAlbedo = _2nd_ShadeColor.rgb * secondShadeTex.rgb;
                
                const float3 color2D = ThreeColorsLinearShading(
                    (baseAlbedo * light2dMod.rgb + light2dAdd.rgb).rgb,
                    (firstShadeAlbedo * light2dMod.rgb + light2dAdd.rgb).rgb,
                    (secondShadeAlbedo * light2dMod.rgb + light2dAdd.rgb).rgb,
                    _BaseTo1st_ShadeStart, _BaseTo1st_ShadeFeather,
                    _1stTo2nd_ShadeStart, _1stTo2nd_ShadeFeather, light2dIntensity);
                
                const float3 directionalLightColorAndUse = _DirectionalLight_Color.rgb * _DirectionalLight_Use; 
                const float3 directionalLightDirection = normalize(-_DirectionalLight_Direction);
                const float dotNL = 0.5 * dot( perturbedNormalWS, directionalLightDirection) + 0.5;

                const float3 toonDiffuseColor = ThreeColorsLinearShading(
                    baseAlbedo * directionalLightColorAndUse,
                    firstShadeAlbedo * directionalLightColorAndUse,
                    secondShadeAlbedo * directionalLightColorAndUse,
                    _BaseTo1st_ShadeStart, _BaseTo1st_ShadeFeather,
                    _1stTo2nd_ShadeStart, _1stTo2nd_ShadeFeather, dotNL);

                const float3 finalDiffuseColor = color2D * _2DLightStrength + toonDiffuseColor * _DirectionalLight_DiffuseStrength;
                
                const float3 viewDirection = normalize(_DirectionalLight_ViewPosition - positionWS);
                const float3 halfDirection = normalize(viewDirection + directionalLightDirection);
                float dotHN_01 = 0.5 * dot(halfDirection,perturbedNormalWS) + 0.5;

                const float highlight = lerp( (1.0 - step(dotHN_01,(1.0 - pow(abs(_DirectionalLight_HighlightSize),5)))), pow(abs(dotHN_01),exp2(lerp(11,1,_DirectionalLight_HighlightSize))), _DirectionalLight_HighlightMode );
                
                const float4 highlightTex = SAMPLE_TEXTURE2D(_HighlightTex, sampler_HighlightTex, uv);
                const float3 highlightAlbedo = highlightTex.rgb * _HighlightColor.rgb; 
                const float3 highlightFactor = directionalLightColorAndUse * _DirectionalLight_HighlightStrength; 
                
                const float3 finalHighlightColor = highlightAlbedo * highlightFactor * highlight;
                const float3 finalColor = _HDREmulationScale * (finalDiffuseColor + finalHighlightColor);

                return float4(finalColor, alpha * _BaseColor.a * _Color.a);
            }

            half4 ToonFragment(Varyings input) : SV_Target {
                // UVs listas para usarse, heredadas del vertex shader
                float2 mainUV = input.uv; 
                
                const half4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, mainUV);
                const half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, mainUV), _BumpScale);

                SurfaceData2D surfaceData;
                const float3 normalWS = normalize(input.normalWS);
                const float3 tangentWS = normalize(input.tangentWS.xyz);
                const float3 bitangentWS = normalize(cross(normalWS, tangentWS) * input.tangentWS.w);

                const float alpha = main.a;
                
                InitializeSurfaceData(main.rgb, alpha, mask, normalTS, surfaceData);

                if (alpha == 0.0) discard;
                
                ShapeLightResult shapeLightResult = CombinedShapeLight(mask, input.lightingUV);
                
                return CombinedShapeLightAndToon(shapeLightResult, surfaceData, mainUV, tangentWS, bitangentWS, normalWS, input.positionWS);
            }
            ENDHLSL
        }

        // ====================================================================================
        // PASS OUTLINE (CONTORNO)
        // ====================================================================================
        Pass {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            #pragma multi_compile TOON_OUTLINE_NORMAL TOON_OUTLINE_POS
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl" 
            
            struct OutlineVertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct OutlineVertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                half2 lightingUV  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };            

            TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);            SAMPLER(sampler_MaskTex);
            TEXTURE2D(_OutlineWidthMap);    SAMPLER(sampler_OutlineWidthMap);
            TEXTURE2D(_OutlineTex);         SAMPLER(sampler_OutlineTex);
            TEXTURE2D(_Outline_NormalMap);  SAMPLER(sampler_Outline_NormalMap);

            #include "Packages/com.unity.toonshader/Runtime/Shaders/URP/ObjectTransform.hlsl"
            #include "Packages/com.unity.toonshader/Runtime/Shaders/URP/ShapeLight2D.hlsl"

            OutlineVertexOutput OutlineVertex(OutlineVertexInput v) {
                OutlineVertexOutput o = (OutlineVertexOutput) 0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // El outline obedece tu script de Tiling/Offset también
                o.uv0 = TRANSFORM_TEX(v.texcoord0, _MainTex);
                
                const float4 objPos = mul (GetObjectToWorldMatrix(), float4(0,0,0,1) );
                const float outlineWidthAlbedo = SAMPLE_TEXTURE2D_LOD(_OutlineWidthMap, sampler_OutlineWidthMap, o.uv0, 0).r;
                const float outlineWidth = _OutlineWidth * 0.001 * outlineWidthAlbedo;
    
                float finalOutlineWidth = outlineWidth * smoothstep( _OutlineFar, _OutlineNear, distance(objPos.rgb,_WorldSpaceCameraPos) );
				float3 newPos; 

#ifdef TOON_OUTLINE_NORMAL
                const float3 normalDir = UnityObjectToWorldNormal(v.normal);
                const float3 tangentDir = normalize( mul( GetObjectToWorldMatrix(), float4( v.tangent.xyz, 0.0 ) ).xyz );
                const float3 bitangentDir = normalize(cross(normalDir, tangentDir) * v.tangent.w);
                float3x3 tangentTransform = float3x3(tangentDir, bitangentDir, normalDir);

                const float4 customNormalMap = SAMPLE_TEXTURE2D_LOD(_Outline_NormalMap, sampler_Outline_NormalMap, o.uv0, 0);
                const float3 normalTS = UnpackNormal(customNormalMap);
                const float3 outlineNormalMapWS = normalize(mul(normalTS.xyz, tangentTransform));
                const float3 outlineDir = lerp(v.normal, outlineNormalMapWS, _Outline_UseNormalMap); 
                
                newPos = v.vertex.xyz + outlineDir * finalOutlineWidth;
                o.pos = TransformObjectToHClip(newPos);
#elif TOON_OUTLINE_POS
                const float3 normalizedPos = normalize(v.vertex.xyz);
                const float signPN = sign(dot(normalizedPos,normalize(v.normal)));
                
                newPos = v.vertex.xyz + signPN * normalizedPos * finalOutlineWidth;
                o.pos = TransformObjectToHClip(newPos);
#endif
                const float scaledOutlineOffsetZ = _OutlineOffsetZ * -0.01;
                o.pos.z = o.pos.z + scaledOutlineOffsetZ;
                o.lightingUV = half2(ComputeScreenPos(o.pos / o.pos.w).xy);
                return o;
            }

            half4 OutlineFragment(OutlineVertexOutput i) : SV_Target {
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv0);
                const half2 lightingUV = i.lightingUV;
                
                float4 _MainTex_var = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv0);
                float3 Set_BaseColor = _BaseColor.rgb * _Color.rgb * _MainTex_var.rgb;
                
                const float3 outlineTex = SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex, i.uv0).rgb;
                const float3 outlineAlbedo = outlineTex * _OutlineColor.rgb;

                const float3 outlineBaseBlend = lerp(outlineAlbedo, outlineAlbedo * Set_BaseColor, _Outline_BaseColorBlend);

                ShapeLightResult shapeLightResult = CombinedShapeLight(mask, lightingUV);
                const float3 color2D = (outlineBaseBlend.rgb * shapeLightResult.mod.rgb) + shapeLightResult.add.rgb;
                const float3 colorToon = outlineBaseBlend.rgb * _DirectionalLight_Color.rgb * _DirectionalLight_Use;
                const float3 outlineLightColor = (color2D * _2DLightStrength) +
                    (colorToon * _DirectionalLight_DiffuseStrength);
                
                const float3 outlineBaseAndLightBlend = lerp(outlineBaseBlend, outlineLightColor, _Outline_LightColorBlend);
                
                return float4(_HDREmulationScale * outlineBaseAndLightBlend,1.0);
            }
            ENDHLSL
        }

        // ====================================================================================
        // PASS NORMALS RENDERING (Para las luces 2D integradas de Unity)
        // ====================================================================================
        Pass{
            Tags{ "LightMode" = "NormalsRendering" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex CustomNormalsVertex
            #pragma fragment NormalsRenderingFragment
            #pragma multi_compile_instancing
            
            struct Attributes { COMMON_2D_NORMALS_INPUTS };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS };
            float4 _White;
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            
            // Función Wrapper para inyectarle el Offset de tu Script a las normales
            Varyings CustomNormalsVertex(Attributes input) { 
                Varyings output = CommonNormalsVertex(input); 
                output.uv = TRANSFORM_TEX(input.uv, _MainTex); 
                return output; 
            }
            
            half4 NormalsRenderingFragment(Varyings input) : SV_Target { return CommonNormalsFragment(input, _White); }
            ENDHLSL
        }

        // ====================================================================================
        // PASS UNLIT (Respaldo)
        // ====================================================================================
        Pass{
            Tags{ "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex CustomUnlitVertex
            #pragma fragment UnlitFragment
            #pragma multi_compile_instancing
            
            struct Attributes { COMMON_2D_INPUTS };
            struct Varyings { COMMON_2D_OUTPUTS };
            float4 _White;
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            
            // Función Wrapper para inyectarle el Offset de tu Script al respaldo visual
            Varyings CustomUnlitVertex(Attributes input) { 
                Varyings output = CommonUnlitVertex(input); 
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output; 
            }
            
            half4 UnlitFragment(Varyings input) : SV_Target { return CommonUnlitFragment(input, _White); }
            ENDHLSL
        }
    }
}