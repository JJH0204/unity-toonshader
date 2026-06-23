#ifndef CHARACTER_TOON_LIGHTING_INCLUDED
#define CHARACTER_TOON_LIGHTING_INCLUDED

// M1 forward shading helpers for toon (cel) lighting.
// Used by ForwardToon pass to compute character light direction, ramp-based shading, and rim.

// Sample ILM map with fallback to neutral values if not used
half4 SampleILM(float2 uv)
{
    half4 ilm = half4(0.0h, 0.5h, 0.5h, 0.0h);
#if defined(_USE_ILM)
    ilm = SAMPLE_TEXTURE2D(_ILMMap, sampler_ILMMap, uv);
#endif
    return ilm;
}

// Compute character light direction, falling back to main light if not set
half3 GetCharacterLightDirection(half3 mainLightDir)
{
    if (_CharacterLightDirWS.w > 0.5h) {
        return normalize(_CharacterLightDirWS.xyz);
    }
    return mainLightDir;
}

// XZ 평면 안전 정규화. 입력이 0(미설정 글로벌)이면 fallback 반환 -> normalize(0,0) NaN 방지.
// Face SDF 경로에서 _FaceForwardWS/_FaceRightWS/라이트 XZ가 0일 때 얼굴이 NaN으로 죽는 것을 막는다.
float2 SafeNormalizeXZ(float2 v, float2 fallback)
{
    float lenSq = dot(v, v);
    return lenSq > 1e-6 ? v * rsqrt(lenSq) : fallback;
}

// 화면공간 UV 미분으로 월드 tangent(±U 방향)를 유도. mesh tangent 채널(아웃라인 스무스 노멀)과 무관.
// M4(T4-2): Angel Ring, Eye Parallax 등 hair flow / eye view space 계산에 사용.
// 유효성: 프래그먼트 스테이지에서만 사용 가능 (ddx/ddy).
float3 GetUVTangentWS(float3 positionWS, float2 uv)
{
    float3 dpdx = ddx(positionWS); float3 dpdy = ddy(positionWS);
    float2 duvdx = ddx(uv);        float2 duvdy = ddy(uv);
    float det = duvdx.x * duvdy.y - duvdy.x * duvdx.y;
    float3 t = (dpdx * duvdy.y - dpdy * duvdx.y) * (det >= 0.0 ? 1.0 : -1.0);
    float lenSq = dot(t, t);
    return lenSq > 1e-8 ? t * rsqrt(lenSq) : float3(1.0, 0.0, 0.0);
}

// WP-B: 픽셀 미분 기반 코탄젠트 프레임으로 탄젠트공간 노멀맵을 월드 N에 적용.
// 메시 TANGENT를 쓰지 않으므로 결정 #3(아웃라인 스무스 노멀을 TANGENT.xyz에 베이크)와 양립한다.
// 프래그먼트에서 ddx/ddy로 프레임을 만들므로 스키닝(변형된 표면)과 미러 UV에 자동 정합.
// 근거: Christian Schüler, "Followup: Normal Mapping Without Precomputed Tangents".
// 유효성: 프래그먼트 스테이지 전용(ddx/ddy). N은 정규화된 월드 노멀, normalTS는 UnpackNormalScale 결과.
half3 ApplyNormalMapDerivative(half3 N, float3 positionWS, float2 uv, half3 normalTS)
{
    float3 dp1 = ddx(positionWS); float3 dp2 = ddy(positionWS);
    float2 duv1 = ddx(uv);        float2 duv2 = ddy(uv);

    // N에 직교하는 코탄젠트 기저(역행렬 없이 cross로 유도)
    float3 dp2perp = cross(dp2, (float3)N);
    float3 dp1perp = cross((float3)N, dp1);
    float3 T = dp2perp * duv1.x + dp1perp * duv2.x;
    float3 B = dp2perp * duv1.y + dp1perp * duv2.y;

    // 퇴화(0 그래디언트) 가드.
    // 주의(2-2 버그 수정): T·B 크기는 "픽셀 풋프린트의 제곱"에 비례해 근거리에서 급격히 작아진다.
    //   구 절대 임계값 `if (maxLenSq < 1e-12) return N;` 은 근거리에서 정상 프레임을 퇴화로 오판해
    //   노멀맵을 통째로 꺼버렸다 → "원거리만 노멀 보이고 근접 시 사라지는" 증상의 1차 원인.
    //   프레임 결과의 *방향*은 invmax 정규화로 스케일 불변이므로, 절대 임계값 대신
    //   0-나눗셈만 막는 near-zero 플로어로 가드한다. 진짜 0 프레임(T=B=0)이면 결과가 ≈N로 자연 폴백.
    float maxLenSq = max(dot(T, T), dot(B, B));
    // 정밀도: TBN은 float3x3로 유지하고 최종 결과만 half로 캐스트(모바일/대월드 안정성, Codex 권장)
    float invmax = rsqrt(max(maxLenSq, 1e-30));
    float3x3 tangentToWorld = float3x3(T * invmax, B * invmax, (float3)N);
    return (half3)normalize(mul((float3)normalTS, tangentToWorld));
}

// L3(갭): 부가광(point/spot/추가 디렉셔널) 1개를 셀 단계화해 가산 기여를 계산.
//   half-Lambert → 1차 그림자 경계(border/blur)로 셀 스텝, 거리·그림자 감쇠 곱. 가산광이므로 그림자 영역엔 0(어둡게 안 함).
//   주의: Light 타입은 URP Lighting.hlsl 제공. Lit ForwardToon은 Lighting.hlsl을 먼저 include하므로 정의됨.
//   Unlit 패스는 Lighting.hlsl을 include하지 않아 Light 타입이 없으므로 가드로 제외(부가광은 Lit 전용).
#ifdef UNIVERSAL_LIGHTING_INCLUDED
half3 ShadeAdditionalToon(Light light, half3 N, half3 albedo, half border, half blur)
{
    half nl  = saturate(dot(N, light.direction) * 0.5h + 0.5h);   // half-Lambert
    half lit = smoothstep(border - blur, border + blur, nl);       // 메인과 동일한 1차 밴드 경계로 셀 스텝
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    return albedo * light.color * (atten * lit);
}
#endif

// L1+: 캐릭터 '추가' 키라이트(2순위 이하)의 가산 셀 기여. 감쇠 없는 방향광이라 URP Light 타입 불필요.
//   1순위 키라이트는 위 cel/SDF 경로가 담당, 추가분만 같은 1차 밴드 경계로 셀 스텝 후 가산.
half3 ShadeCharacterExtraToon(half3 dirWS, half3 color, half3 N, half3 albedo, half border, half blur)
{
    half nl  = saturate(dot(N, dirWS) * 0.5h + 0.5h);             // half-Lambert
    half lit = smoothstep(border - blur, border + blur, nl);
    return albedo * color * lit;
}

// L1+: _CharacterExtraLight* 배열을 순회하며 가산 셀 기여를 합산.
half3 AccumulateCharacterExtraLights(half3 N, half3 albedo, half border, half blur)
{
    half3 sum = (half3)0.0h;
    int count = (int)_CharacterExtraLightCount;
    [loop] for (int i = 0; i < CHARACTER_EXTRA_LIGHT_MAX; i++)
    {
        if (i >= count) break;
        half3 dir = (half3)normalize(_CharacterExtraLightDir[i].xyz);
        sum += ShadeCharacterExtraToon(dir, (half3)_CharacterExtraLightColor[i].rgb, N, albedo, border, blur);
    }
    return sum;
}

#endif // CHARACTER_TOON_LIGHTING_INCLUDED
