// Capa de oscuridad de la noche con la linterna del cinturón de Lucas.
// La usa Linterna.cs sobre una RawImage que cubre la pantalla (UV 0..1).
Shader "Juego/Oscuridad"
{
    Properties
    {
        [PerRendererData] _MainTex ("Textura", 2D) = "white" {}
        _Color ("Oscuridad", Color) = (0.02, 0.03, 0.09, 0.62)
        _Calido ("Luz calida", Color) = (1, 0.85, 0.5, 0.14)
        _Lente ("Lente (uv xy, radio halo, aspecto)", Vector) = (0.5, 0.3, 0.05, 1.777)
        _Haz ("Haz (dir xy, largo, apertura)", Vector) = (1, 0, 0.5, 0.5)
        _Cuerpo ("Cuerpo (uv xy, radio, brillo)", Vector) = (0.5, 0.3, 0.2, 0.35)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha   // premultiplicado: la oscuridad tapa, la luz cálida suma

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            fixed4 _Color;
            fixed4 _Calido;
            float4 _Lente;
            float4 _Haz;
            float4 _Cuerpo;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float aspecto = _Lente.w;

                // brillo pequeño en la lente
                float2 p = i.uv - _Lente.xy;
                p.x *= aspecto;
                float halo = 1.0 - smoothstep(_Lente.z * 0.3, _Lente.z, length(p));

                // haz hacia adelante, que nace angosto en la lente y se abre
                float a = dot(p, _Haz.xy);
                float b = abs(p.x * _Haz.y - p.y * _Haz.x);
                float ancho = a * _Haz.w + 0.012;
                float cono = step(0.0, a)
                           * (1.0 - smoothstep(ancho * 0.5, ancho, b))
                           * (1.0 - smoothstep(_Haz.z * 0.55, _Haz.z, a));

                // resplandor suave sobre Lucas, para que se vea
                float2 q = i.uv - _Cuerpo.xy;
                q.x *= aspecto;
                float cuerpo = (1.0 - smoothstep(_Cuerpo.z * 0.4, _Cuerpo.z, length(q))) * _Cuerpo.w;

                float linterna = saturate(max(halo, cono));
                float luz = max(linterna, cuerpo);
                float oscuro = _Color.a * (1.0 - luz) * i.color.a;
                fixed4 c;
                c.rgb = _Color.rgb * oscuro + _Calido.rgb * (_Calido.a * linterna * i.color.a);
                c.a = oscuro;
                return c;
            }
            ENDCG
        }
    }
}
