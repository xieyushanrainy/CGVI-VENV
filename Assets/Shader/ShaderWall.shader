Shader "Custom/ShaderWall"
{
     SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
 
            appdata_full vert (appdata_full v)
            {
                v.vertex = UnityObjectToClipPos(v.vertex);
                return v;
            }
 
            float4 frag (appdata_full v) : SV_Target
            {
                float3 worldNormal = v.normal; // 世界坐标系下的法线
                float3 worldLight = normalize(_WorldSpaceLightPos0.xyz); // 世界坐标系下的光线方向
 
                // 获取环境光
                float3 anbient = 3;
 
                // 根据半兰伯特模型计算反射光
                float halfLamient = dot(worldNormal, worldLight) * 0.5 + 0.5;
                float3 diffuse = _LightColor0.rgb * v.color.rgb * halfLamient;
 
                // 反射光加光照强度
                float3 c = anbient + diffuse;
 
                return float4(c,1);
            }
            ENDCG
        }
    }
}
