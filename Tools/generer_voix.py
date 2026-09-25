#!/usr/bin/env python3
"""
Genere les voix des repliques du jeu (Assets/UberBagarre/Resources/Voix/*.ogg).

Chaque replique FIXE du scenario (PrologueDirector.cs) est lue par une voix de synthese
neuronale hors-ligne (Piper, voix francaises « gilles » et « siwis »), puis traitee selon
le personnage : hauteur, debit, filtre de combine telephonique pour Sami et l'appli.

Le jeu retrouve le fichier par une EMPREINTE de « PERSONNAGE|texte » (FNV-1a 32 bits sur
les unites UTF-16, identique a DialogueVoice.Key en C#). Une replique dont le texte depend
du jeu (une somme d'argent) n'a pas de fichier : le jeu la « babille » a la place.

Usage :  pip install piper-tts soundfile scipy numpy
         python3 Tools/generer_voix.py
Les modeles de voix sont telecharges au premier lancement (dossier Tools/.voix, ignore par git).
"""
import json, os, re, sys, tarfile, urllib.request, wave, io
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "Assets/UberBagarre/Scripts/Story/PrologueDirector.cs")
OUT = os.path.join(ROOT, "Assets/UberBagarre/Resources/Voix")
CACHE = os.path.join(ROOT, "Tools/.voix")

MODELS = {
    "gilles": "https://github.com/rhasspy/piper/releases/download/v0.0.2/voice-fr-gilles-low.tar.gz",
    "siwis": "https://github.com/rhasspy/piper/releases/download/v0.0.2/voice-fr-siwis-medium.tar.gz",
}

# Personnage -> (voix, hauteur, debit, filtre)
CAST = {
    "MOI":        ("gilles", 1.00, 1.00, None),
    "SAMI":       ("gilles", 1.12, 1.08, "telephone"),
    "APPLI":      ("siwis",  1.02, 1.06, "appli"),
    "DRAGAN":     ("gilles", 0.88, 0.95, None),
    "MILAN":      ("gilles", 0.97, 1.10, None),
    "LE TAUREAU": ("gilles", 0.80, 0.90, None),
}

# Valeurs des variables du scenario, par chapitre (celles que construit le generateur de scene).
CONTEXT = {
    "prologue": {"target": "BRUNO MORETTI", "clothing": "Veste rouge, jean clair", "friend": "SAMI"},
    "chapitre1": {"targets": "LES FRÈRES KOVAC", "clothing": "Survêtements, un noir, un bordeaux",
                  "elder": "DRAGAN", "younger": "MILAN", "friend": "SAMI"},
    "chapitre2": {"champion": "LE TAUREAU", "clothing": "Débardeur noir, treillis kaki", "friend": "SAMI"},
}


def key(speaker, text):
    """FNV-1a 32 bits sur les unites UTF-16 — identique a DialogueVoice.Key (C#)."""
    data = (speaker.upper() + "|" + text).encode("utf-16-le")
    h = 0x811C9DC5
    for i in range(0, len(data), 2):
        unit = data[i] | (data[i + 1] << 8)
        h ^= unit
        h = (h * 0x01000193) & 0xFFFFFFFF
    return "v_%08x" % h


def extract():
    """Toutes les repliques dont le texte est connu a l'avance."""
    src = open(SOURCE, encoding="utf-8").read()
    ch1 = src.index("private void AddChapterOne(")
    ch2 = src.index("private void AddChapterTwo(")

    lines = []
    pattern = re.compile(r'(?:\.Say|DialogueLine\.Say|(?<![\w.])Say)\(\s*([^,]+?)\s*,\s*((?:[^;)(]|\([^()]*\))+?)\)(?=\s*[;)\n.])')
    for m in pattern.finditer(src):
        if "private void Say" in src[max(0, m.start() - 40):m.start() + 5]:
            continue
        pos = m.start()
        ctx = CONTEXT["prologue"] if pos < ch1 else CONTEXT["chapitre1"] if pos < ch2 else CONTEXT["chapitre2"]

        speaker = evaluate(m.group(1), ctx)
        text = evaluate(m.group(2), ctx)
        if speaker is None or text is None or speaker in ("speaker",):
            continue
        lines.append((speaker, text))

    seen = set()
    unique = []
    for s, t in lines:
        if (s, t) in seen:
            continue
        seen.add((s, t))
        unique.append((s, t))
    return unique


def evaluate(expr, ctx):
    """Evalue une concatenation C# de litteraux et de variables connues. None si inconnue."""
    expr = expr.strip()
    expr = re.sub(r'\(\s*_briefing\w*\s*!=\s*null\s*\?\s*_briefing\w*\.Reward\s*:\s*(\d+)\s*\)', r'\1', expr)
    parts = re.findall(r'"((?:[^"\\]|\\.)*)"|([A-Za-z_]\w*)|(\d+)|(\+)|(\S)', expr)
    out = ""
    for lit, ident, num, plus, other in parts:
        if plus:
            continue
        if other:
            return None
        if lit or (lit == "" and not ident and not num):
            out += lit.replace('\\"', '"').replace("\\\\", "\\")
        elif num:
            out += num
        elif ident:
            if ident not in ctx:
                return None
            out += ctx[ident]
    return out


def ensure_models():
    os.makedirs(CACHE, exist_ok=True)
    paths = {}
    for name, url in MODELS.items():
        onnx = [f for f in os.listdir(CACHE) if f.endswith(".onnx") and name in f]
        if not onnx:
            print("Telechargement de la voix", name, "...")
            data = urllib.request.urlopen(url).read()
            with tarfile.open(fileobj=io.BytesIO(data)) as tar:
                tar.extractall(CACHE)
            onnx = [f for f in os.listdir(CACHE) if f.endswith(".onnx") and name in f]
        paths[name] = os.path.join(CACHE, onnx[0])
    return paths


def speakable(text):
    """Texte lu par la synthese : quelques abreviations que la voix massacrerait."""
    t = text.replace("K.O.", "K-O").replace("RDV", "R.D.V.").replace("Über", "Uber")
    t = t.replace("…", ".").replace("—", ",")
    # Les noms en capitales seraient epeles lettre a lettre : on les lit comme des mots.
    t = re.sub(r"\b([A-ZÀ-Ý]{4,})\b", lambda m: m.group(1).capitalize(), t)
    return t


def synthesize(voice, text, length_scale):
    from piper import SynthesisConfig
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as w:
        voice.synthesize_wav(text, w, syn_config=SynthesisConfig(length_scale=length_scale, noise_scale=0.6, noise_w_scale=0.75))
    buffer.seek(0)
    with wave.open(buffer, "rb") as r:
        rate = r.getframerate()
        pcm = np.frombuffer(r.readframes(r.getnframes()), dtype=np.int16).astype(np.float32) / 32768.0
    return pcm, rate


def process(pcm, rate, pitch, character_filter):
    from scipy import signal
    # Hauteur : on a synthetise plus lentement, on reechantillonne pour remonter la voix.
    if abs(pitch - 1.0) > 1e-3:
        n = int(round(len(pcm) / pitch))
        pcm = signal.resample(pcm, n).astype(np.float32)

    if character_filter == "telephone":
        sos = signal.butter(4, [320, 3300], btype="bandpass", fs=rate, output="sos")
        pcm = signal.sosfilt(sos, pcm)
        pcm = np.tanh(pcm * 2.2) / np.tanh(2.2)
        pcm += np.random.default_rng(7).normal(0, 0.004, len(pcm))
    elif character_filter == "appli":
        sos = signal.butter(2, [160, 7000], btype="bandpass", fs=rate, output="sos")
        pcm = signal.sosfilt(sos, pcm)

    # Petite respiration avant, fin propre apres.
    pcm = np.concatenate([np.zeros(int(rate * 0.04)), pcm, np.zeros(int(rate * 0.08))])
    peak = np.max(np.abs(pcm)) or 1.0
    return (pcm / peak * 0.8).astype(np.float32)


def main():
    import soundfile as sf
    from piper import PiperVoice

    lines = extract()
    print(len(lines), "repliques a voix")

    models = ensure_models()
    voices = {name: PiperVoice.load(path) for name, path in models.items()}

    os.makedirs(OUT, exist_ok=True)
    keep = set()
    listing = []

    for speaker, text in lines:
        if not re.search(r"[A-Za-zÀ-ÿ]", text):
            continue

        cast = CAST.get(speaker.upper())
        if cast is None:
            print("  (pas de voix pour", speaker, ")")
            continue

        name, pitch, tempo, character_filter = cast
        k = key(speaker, text)
        keep.add(k + ".ogg")
        listing.append("%s  %-10s %s" % (k, speaker, text))

        path = os.path.join(OUT, k + ".ogg")
        if os.path.exists(path) and "--force" not in sys.argv:
            continue

        pcm, rate = synthesize(voices[name], speakable(text), pitch / tempo)
        pcm = process(pcm, rate, pitch, character_filter)
        sf.write(path, pcm, rate, format="OGG", subtype="VORBIS")
        print("  ", k, speaker, ":", text)

    # Les voix de repliques disparues du scenario sont retirees.
    for f in os.listdir(OUT):
        if f.endswith(".ogg") and f not in keep:
            os.remove(os.path.join(OUT, f))

    with open(os.path.join(OUT, "_liste.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(sorted(listing)) + "\n")

    print("Termine :", len(keep), "fichiers dans", OUT)


if __name__ == "__main__":
    main()
