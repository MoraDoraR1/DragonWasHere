// 건물·유닛 스프라이트 효과 (가이드 5장 "없는 리소스와 대체 처리").
// _Gold: 원래 그림의 밝기를 황금 그라데이션으로 바꾼다 (보너스 황금 건물, 감별안 황금화). 황금 스프라이트가 없어서 쓴다.
// _Flash: 피격 시 흰 플래시. 그림 모양 그대로 흰색에 가깝게 칠한다.
// 값은 SpriteRenderer마다 MaterialPropertyBlock으로 넣는다. URP 2D 렌더러에서 조명 없이 그린다.
Shader "DWH/SpriteFx"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Gold ("Gold", Range(0, 1)) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _GoldDark ("Gold Dark", Color) = (0.55, 0.32, 0.05, 1)
        _GoldLight ("Gold Light", Color) = (1, 0.93, 0.55, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _Gold;
            float _Flash;
            fixed4 _FlashColor;
            fixed4 _GoldDark;
            fixed4 _GoldLight;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                fixed3 gold = lerp(_GoldDark.rgb, _GoldLight.rgb, saturate(lum * 1.25));
                c.rgb = lerp(c.rgb, gold, _Gold);
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _Flash);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
