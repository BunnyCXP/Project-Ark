Shader "TheGlitch/AnimusClippingShader"
{
    Properties
    {
        _Color ("Base Color (基础颜色)", Color) = (1,1,1,1)
        _MainTex ("Albedo (贴图)", 2D) = "white" {}
        _Glossiness ("Smoothness (光滑度)", Range(0,1)) = 0.5
        _Metallic ("Metallic (金属度)", Range(0,1)) = 0.0
        
        [HDR] _EdgeGlowColor ("Edge Glow Color (切面发光色)", Color) = (0.2, 0.6, 1.0, 1.0)
        _EdgeWidth ("Edge Width (切面发光厚度)", Range(0.01, 2.0)) = 0.15
    }
    SubShader
    {
        // 设置为不透明物体，正常接收光影
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        
        // 如果你切开物体后不想看到模型内部的空心，可以把下面这行前面的 // 去掉
        // Cull Off 

        CGPROGRAM
        // 【👑 核心升级】：使用 Standard 物理光照模型，并且开启全阴影支持 (fullforwardshadows)
        #pragma surface surf Standard fullforwardshadows

        // 使用高精度渲染
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos; // 获取世界坐标用于计算距离
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float4 _EdgeGlowColor;
        float _EdgeWidth;

        // 全局控制器 (由 C# 脚本发送)
        float3 _GlobalAnimusOrigin;
        float _GlobalAnimusRadius;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 1. 计算当前像素距离扫描原点有多远
            float dist = distance(IN.worldPos, _GlobalAnimusOrigin);

            // 2. 【核心裁剪】：超出的部分直接切掉！
            clip(_GlobalAnimusRadius - dist);

            // 3. 【恢复物理光照】：读取贴图颜色和材质属性
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;

            // 4. 【激光切面发光】：只在边缘产生高亮自发光 (Emission)
            if (dist > _GlobalAnimusRadius - _EdgeWidth)
            {
                // 计算越靠近边缘，光越亮
                float edgeFactor = (_GlobalAnimusRadius - dist) / _EdgeWidth;
                o.Emission = _EdgeGlowColor.rgb * (1.0 - edgeFactor) * 3.0; // 乘以 3.0 增加爆闪亮度
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}