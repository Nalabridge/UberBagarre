// Über Bagarre — le ciel, en fonctions pures : le shader du ciel l'affiche, l'eau s'en sert pour
// ses reflets (une mer qui reflète un autre ciel que celui qu'on voit, ça se remarque tout de suite).
//
// Écrit sans rien de propre à un moteur de rendu (float, fonctions HLSL de base) : inclus aussi
// bien par un shader CG (le ciel) que par une passe URP (l'eau).
//
// Entrées communes :
//   dir      direction de vue (normalisée, monde)
//   sunDir   direction VERS le soleil (normalisée) — sous l'horizon la nuit
//   night    0 jour → 1 nuit noire
//   overcast 0 dégagé → 1 couvert (la météo)
#ifndef UBER_SKY_INCLUDED
#define UBER_SKY_INCLUDED

float UberSkyHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float UberSkyNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = UberSkyHash(i);
    float b = UberSkyHash(i + float2(1, 0));
    float c = UberSkyHash(i + float2(0, 1));
    float d = UberSkyHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Bruit fractal ; chaque octave est tournée pour casser les alignements de la grille.
float UberSkyFbm(float2 p, int octaves)
{
    float v = 0.0;
    float a = 0.5;
    float norm = 0.0;
    for (int k = 0; k < 5; k++)
    {
        if (k >= octaves) break;
        v += UberSkyNoise(p) * a;
        norm += a;
        p = float2(p.x * 1.6 - p.y * 1.2, p.x * 1.2 + p.y * 1.6) + 17.13;
        a *= 0.5;
    }

    return v / norm;
}

// À quel point le soleil est au crépuscule (bas sur l'horizon) : 0 en plein jour comme la nuit.
float UberSkyDusk(float3 sunDir)
{
    return saturate(1.0 - abs(sunDir.y - 0.04) * 5.0);
}

float3 UberSkySunColor(float3 sunDir)
{
    return lerp(float3(1.0, 0.96, 0.9), float3(1.0, 0.55, 0.3), UberSkyDusk(sunDir));
}

// Le dégradé de l'atmosphère : bleu profond au zénith, pâle à l'horizon ; au crépuscule l'horizon
// s'embrase du côté du soleil ; la nuit, un bleu presque noir ; sous la pluie, un gris uniforme.
float3 UberSkyGradient(float3 dir, float3 sunDir, float night, float overcast)
{
    float up = saturate(dir.y);
    float h = pow(1.0 - up, 3.0);
    float dusk = UberSkyDusk(sunDir);

    float3 zenithDay = float3(0.16, 0.38, 0.80);
    float3 horizonDay = float3(0.64, 0.78, 0.94);
    float3 dayCol = lerp(zenithDay, horizonDay, h);

    float2 flatDir = dir.xz + 1e-4;
    float2 flatSun = sunDir.xz + 1e-4;
    float toward = saturate(dot(normalize(flatDir), normalize(flatSun)) * 0.5 + 0.5);
    float3 zenithDusk = float3(0.14, 0.2, 0.42);
    float3 horizonDusk = lerp(float3(0.42, 0.36, 0.52), float3(1.0, 0.5, 0.25), toward * toward);
    float3 duskCol = lerp(zenithDusk, horizonDusk, h);

    float3 col = lerp(dayCol, duskCol, dusk * (1.0 - night * 0.6));

    float3 nightCol = lerp(float3(0.004, 0.008, 0.022), float3(0.018, 0.026, 0.05), h);
    col = lerp(col, nightCol, night);

    // Sous l'horizon : le sol voilé de brume, jamais un trou noir.
    float3 below = lerp(horizonDay * 0.55, float3(0.025, 0.03, 0.05), night);
    col = lerp(col, below, saturate(-dir.y * 5.0));

    float3 grey = lerp(float3(0.52, 0.55, 0.6), float3(0.025, 0.03, 0.04), night);
    col = lerp(col, grey * lerp(0.9, 1.05, h), overcast * 0.85);

    // Le halo du soleil (diffusion vers l'avant), plus large et plus chaud au couchant.
    float mu = saturate(dot(dir, sunDir));
    float glow = pow(mu, 6.0) * (0.25 + dusk * 0.35) + pow(mu, 48.0) * 0.5;
    col += UberSkySunColor(sunDir) * glow * (1.0 - night) * (1.0 - overcast * 0.75);
    return col;
}

// Les nuages : des cumulus (une couche de bruit déformé) éclairés comme un volume — la lumière
// qui traverse moins de nuage vers le soleil rend le côté soleil blanc et la base grise, les bords
// fins s'illuminent à contre-jour — et des cirrus en altitude. Renvoie la couleur et l'opacité.
float UberSkyCloudDensity(float2 q, float cover)
{
    float n = UberSkyFbm(q, 5);
    n = (n - 0.5) * 2.0 + 0.5;
    return smoothstep(1.0 - cover, 1.0 - cover + 0.35, n);
}

float4 UberSkyClouds(float3 dir, float3 sunDir, float night, float overcast, float time, float coverage, float speed, float scale)
{
    if (dir.y <= 0.0) return float4(0, 0, 0, 0);

    float2 plane = dir.xz / (dir.y + 0.1);
    float2 wind = float2(1.0, 0.35) * time * speed;

    float2 p = plane * scale + wind;
    float2 warp = float2(UberSkyFbm(p * 0.5 + 3.1, 3), UberSkyFbm(p * 0.5 + 7.7, 3));
    float2 q = p + warp * 1.1;

    float cover = saturate(coverage + overcast * 0.5);
    float density = UberSkyCloudDensity(q, cover);

    // Combien de nuage entre ce point et le soleil (deux pas vers lui).
    float2 toSun = normalize(sunDir.xz + 1e-4) * 0.18;
    float d1 = UberSkyCloudDensity(q + toSun, cover);
    float d2 = UberSkyCloudDensity(q + toSun * 2.2, cover);
    float transmit = exp(-(d1 * 1.3 + d2 * 0.8));
    float powder = 1.0 - exp(-density * 3.0);
    float light = saturate(transmit * 0.9 + 0.1) * lerp(0.75, 1.0, powder);
    light = lerp(light, 0.5, saturate(-sunDir.y * 4.0));

    float dusk = UberSkyDusk(sunDir);
    float3 sunCol = UberSkySunColor(sunDir);
    float3 ambient = lerp(float3(0.46, 0.54, 0.66), float3(0.38, 0.30, 0.40), dusk);
    float3 cloud = ambient * (0.6 + 0.4 * (1.0 - density)) + sunCol * light * 1.15;

    float mu = saturate(dot(dir, sunDir));
    cloud += sunCol * pow(mu, 8.0) * (1.0 - density) * 1.4 * (1.0 - night);

    // Cirrus : de longs voiles fins, très hauts, qui avancent plus vite.
    float2 pc = plane * scale * 0.35 + wind * 1.7;
    float ci = UberSkyFbm(float2(pc.x * 2.4, pc.y * 0.6), 3);
    float cirrus = smoothstep(0.56, 0.86, ci) * 0.45 * (1.0 - overcast);

    cloud = lerp(cloud, cloud * float3(0.035, 0.04, 0.065), night);
    cloud = lerp(cloud, float3(0.5, 0.52, 0.56) * lerp(1.0, 0.06, night), overcast * 0.6);

    float fade = smoothstep(0.0, 0.2, dir.y);
    float alpha = saturate(density * 0.97 + cirrus * (1.0 - density)) * fade;
    return float4(cloud, alpha);
}

// Le ciel complet sans le disque du soleil ni les étoiles (pour les reflets de l'eau).
float3 UberSkyReflection(float3 dir, float3 sunDir, float night, float overcast, float time)
{
    float3 col = UberSkyGradient(dir, sunDir, night, overcast);
    float4 clouds = UberSkyClouds(dir, sunDir, night, overcast, time, 0.5, 0.012, 1.25);
    return lerp(col, clouds.rgb, clouds.a);
}

#endif
