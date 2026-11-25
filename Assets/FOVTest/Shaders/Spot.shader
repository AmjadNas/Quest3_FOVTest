// Disc or ring drawn by true visual angle from the stage centre, shown to one eye or both.
// The edge is a raised cosine of width _EdgeDeg centred on the radius, so the nominal radius is at half height.
Shader "FOVTest/Spot"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _RadiusDeg ("Outer radius (deg)", Float) = 0.215
        _InnerDeg ("Inner radius (deg, 0 = disc)", Float) = 0
        _EdgeDeg ("Edge width (deg)", Float) = 0.1
        _TanHalf ("tan(half angle of quad)", Float) = 0.01
        _EyeMask ("0 both, 1 left, 2 right", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _RadiusDeg, _InnerDeg, _EdgeDeg, _TanHalf, _EyeMask;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                // Collapse the quad for the eye that must not see it.
                bool hidden = (_EyeMask > 0.5 && _EyeMask < 1.5 && unity_StereoEyeIndex == 1)
                           || (_EyeMask > 1.5 && unity_StereoEyeIndex == 0);
                if (hidden) o.pos = float4(0, 0, -2, 1);
                return o;
            }

            float edge(float r, float radius)
            {
                float t = saturate((r - (radius - _EdgeDeg * 0.5)) / max(_EdgeDeg, 1e-4));
                return 0.5 + 0.5 * cos(t * UNITY_PI);   // 1 inside, 0 outside
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = degrees(atan(length(i.uv) * _TanHalf));
                float a = edge(r, _RadiusDeg);
                if (_InnerDeg > 0) a *= 1 - edge(r, _InnerDeg);
                return fixed4(_Color.rgb, _Color.a * a);
            }
            ENDCG
        }
    }
}
