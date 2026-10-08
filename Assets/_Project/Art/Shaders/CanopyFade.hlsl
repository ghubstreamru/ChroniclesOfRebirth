// Бледнение крон рядом с героем и растворение листвы у камеры — общее для билбордов (PixelSprite) и объёмной листвы (PixelFoliage).
// Подключать после CBUFFER материала: нужны _CrownFade и _FadeMode.
#ifndef COR_CANOPY_FADE_INCLUDED
#define COR_CANOPY_FADE_INCLUDED

// Глобальные значения от CanopyFadeController: позиция героя (w = 1, если активен),
// горизонтальный радиус бледнения (шире в густом лесу) и запас вокруг линии «камера → герой».
float4 _CoR_PlayerPosition;
float _CoR_CanopyRadius;
float _CoR_OccludeMargin;
// От камеры (Hd2dFollow): листва ближе x юнитов к камере начинает растворяться, ближе y — исчезает (0 — выключено).
float4 _CoR_CameraFade;

// 0 — крона непрозрачна, 1 — полностью «отбелена». anchor: xyz — центр кроны, w — её радиус (одинаковы для всей кроны),
// поэтому дерево бледнеет целиком.
half CrownFade(float4 anchor)
{
    if (_CrownFade < 0.5 || _CoR_PlayerPosition.w < 0.5)
        return 0;

    float3 anchorWS = anchor.xyz;
    float3 player = _CoR_PlayerPosition.xyz;
    float3 cameraWS = GetCameraPositionWS();

    // Крона рядом с героем по горизонтали. Зона вытянута к камере: камера смотрит сверху под углом,
    // поэтому героя закрывают высокие кроны в нескольких метрах ближе к камере, а не соседние.
    float2 toPlayer = player.xz - cameraWS.xz;
    float2 forward = toPlayer / max(length(toPlayer), 1e-4);
    float2 rel = anchorWS.xz - player.xz;
    float along = dot(rel, forward);                   // < 0 — между героем и камерой
    float side = dot(rel, float2(-forward.y, forward.x));
    float stretched = length(float2(side, along < 0 ? along * 0.5 : along * 1.5));
    float nearFade = saturate(1 - (stretched - _CoR_CanopyRadius) / 2.0);

    // Крона задевает линию взгляда «камера → герой» с учётом своего радиуса: бледнеют все кроны
    // на этой линии (в том числе ряд деревьев и крона, закрывающая героя только краем).
    float3 ray = player - cameraWS;
    float t = saturate(dot(anchorWS - cameraWS, ray) / dot(ray, ray));
    float distanceToRay = distance(anchorWS, cameraWS + ray * t);
    float beforePlayer = step(t, 0.97); // кроны позади героя не трогаем
    float occlusion = beforePlayer * saturate(1 - (distanceToRay - anchor.w - _CoR_OccludeMargin));

    return max(nearFade, occlusion);
}

// 0 — элемент листвы далеко от камеры, 1 — вплотную (полностью растворён). Считается по его центру (pivot), поэтому
// клубок или карточка растворяется целиком, без точечного «шума» по краям.
half CameraFade(float3 pivotWS)
{
    if (_CrownFade < 0.5 || _CoR_CameraFade.x <= 0)
        return 0;
    float d = distance(pivotWS, GetCameraPositionWS());
    return saturate((_CoR_CameraFade.x - d) / max(_CoR_CameraFade.x - _CoR_CameraFade.y, 1e-3));
}

// Элемент у камеры всегда уходит в бледный режим, даже если крона в целом непрозрачна.
half CombinedFade(float4 anchor, half cameraFade)
{
    return max(CrownFade(anchor), cameraFade > 0.001 ? 1 : 0);
}

// Видимость фрагмента для текущего режима материала: в режиме 0 бледнеющие кроны отсекаются, в режиме 1 — остальные.
void CrownClip(half fade)
{
    if (_CrownFade < 0.5)
        return;
    clip(_FadeMode < 0.5 ? 0.5 - fade : fade - 0.5);
}

#endif
