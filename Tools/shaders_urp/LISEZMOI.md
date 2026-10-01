# Vérifier les shaders URP hors d'Unity

Un banc de test pour compiler les passes URP de nos shaders sans ouvrir Unity, contre la vraie
bibliothèque de shaders d'URP 17.0 (Unity 6000.0), avec `glslangValidator` (frontal HLSL).

```
apt-get install glslang-tools          # une fois
mkdir travail && cd travail
python3 ../crawl.py Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl \
    Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl
ln -s <dépôt>/Assets root/Assets
python3 ../autofix.py passe.hlsl frag frag INTRINSIC_WAVEREADFIRSTLANE SHADER_API_VULKAN UNITY_VERSION=600000 SHADER_TARGET=35
python3 ../check.py <dépôt>/Assets/UberBagarre/Art/Shaders/UberSkin.shader <dépôt>/Tools/schedule1/shaders/*.shader
```

- `crawl.py` télécharge les fichiers inclus (branche `6000.0/staging` du dépôt Graphics d'Unity).
- glslang ne distingue pas `half` de `float` : `autofix.py` retire, **dans cette copie locale
  seulement**, les surcharges `half` en double de la bibliothèque (à lancer une fois sur une passe).
- `check.py` extrait chaque passe du SubShader URP et la compile en sommet et en fragment, pour
  chaque mot-clé de ses `multi_compile` et pour un variant réaliste (ombres en cascades, Forward+,
  ombres douces, SSAO, brume).

Ce n'est pas le compilateur d'Unity (FXC/DXC) : une passe qui compile ici peut encore échouer sur
une règle propre à une plateforme, mais les fautes de frappe, les fonctions ou champs absents de la
version d'URP et les erreurs de types sont attrapés.
