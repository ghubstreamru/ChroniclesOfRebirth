// Общее для шейдеров земли (TerrainBlend) и травяного покрова (PixelGrass): шум и чтение карты типов земли
// с неровными, «природными» границами. Подключать после CBUFFER материала: нужны _MapRect, _MapTexel,
// _TexelsPerUnit, _EdgeNoise, _EdgeDetail.
#ifndef COR_GROUND_COMMON_INCLUDED
#define COR_GROUND_COMMON_INCLUDED

TEXTURE2D(_GroundMap);
SAMPLER(sampler_GroundMap);

// Индексы слоёв массива текстур земли = значения GroundType; после них — текстуры обрывов.
#define GROUND_DEEP_BED 0
#define GROUND_SHALLOW_BED 1
#define GROUND_PEBBLES 2
#define GROUND_GRASS 3
#define GROUND_GRASS_LEAVES 4
#define GROUND_FOREST_FLOOR 5
#define GROUND_DIRT 6
#define GROUND_FLAGSTONE 7
#define GROUND_ROCK 8
#define GROUND_SNOW 9
#define GROUND_CLIFF_EARTH 10
#define GROUND_CLIFF_ROCK 11

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3 - 2 * f);
    float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Точка, привязанная к пиксельной сетке текстур (центр пикселя): всё, что считается от неё, не дробит пиксель.
float2 SnapToTexel(float2 xz)
{
    return (floor(xz * _TexelsPerUnit) + 0.5) / _TexelsPerUnit;
}

// Тип земли в точке: позиция привязывается к пиксельной сетке, затем сдвигается шумом
// (крупные «языки» + мелкая рваность) и по ней читается карта клеток.
float GroundIndex(float2 xz)
{
    float2 snapped = SnapToTexel(xz);
    float2 jitter = float2(ValueNoise(snapped * 1.3), ValueNoise(snapped * 1.3 + 17.7)) - 0.5;
    float2 fine = float2(ValueNoise(snapped * 7.0 + 3.1), ValueNoise(snapped * 7.0 + 9.4)) - 0.5;
    float2 p = snapped + jitter * 2 * _EdgeNoise + fine * 2 * _EdgeDetail;
    float2 uv = (p - _MapRect.xy) / _MapRect.zw;
    uv = (floor(uv * _MapTexel.zw) + 0.5) * _MapTexel.xy;
    return floor(SAMPLE_TEXTURE2D_LOD(_GroundMap, sampler_GroundMap, uv, 0).r * 16 + 0.5);
}

#endif
