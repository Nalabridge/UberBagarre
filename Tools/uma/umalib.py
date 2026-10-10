"""
Lecture du paquet UMA 2 (Unity Multipurpose Avatar, gratuit sur l'Asset Store) hors d'Unity.

Un .unitypackage est une archive tar.gz : un dossier par asset (nommé par son GUID) avec le
fichier lui-même (« asset »), sa méta et son chemin (« pathname »). Les assets UMA (slots =
maillages, overlays = textures) sont des fichiers sérialisés Unity binaires avec arbre de
types : UnityPy les relit sans Unity.

    extract(paquet, cache)   indexe le paquet et en sort les fichiers utiles dans <cache>
    Slot(chemin)             un maillage UMA : sommets, normales, UV, triangles, os, poids
    Overlay(chemin)          une couche de texture UMA : textures et rectangle dans l'atlas
    Skeleton(slots)          les os réunis de plusieurs slots, pose de liaison en repère monde
    skin(slot, squelette)    les sommets d'un slot posés sur le squelette (éventuellement
                             déformé : c'est ainsi que se règlent les morphologies)
"""
import json
import os
import tarfile

import numpy as np

PREFIX = 'Assets/UMA/UMA2/'
WANTED = ('.asset', '.png', '.tga', '.psd', '.fbx', '.mat', '.txt')

_files = None
_index = None


def extract(package, cache):
    """Indexe le paquet (GUID -> chemin) et extrait les fichiers utiles, une seule fois."""
    global _files, _index
    _files = os.path.join(cache, 'files')
    index_path = os.path.join(cache, 'index.json')
    if not os.path.exists(index_path):
        os.makedirs(_files, exist_ok=True)
        names, members = {}, {}
        with tarfile.open(package, 'r:gz') as tar:
            for m in tar:
                parts = m.name.strip('./').split('/')
                if len(parts) != 2:
                    continue
                guid, kind = parts
                if kind == 'pathname':
                    names[guid] = tar.extractfile(m).read().decode('utf-8', 'replace').splitlines()[0]
                elif kind == 'asset':
                    members[guid] = m.name
        rows = [(names[g], g) for g in names if g in members]
        wanted = {members[g]: p for p, g in rows if p.lower().endswith(WANTED) and p.startswith(PREFIX)}
        with tarfile.open(package, 'r:gz') as tar:
            for m in tar:
                p = wanted.get(m.name)
                if p is None:
                    continue
                dst = os.path.join(_files, p[len(PREFIX):])
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                with open(dst, 'wb') as f:
                    f.write(tar.extractfile(m).read())
        with open(index_path, 'w') as f:
            json.dump(rows, f)
    with open(index_path) as f:
        _index = {g: p[len(PREFIX):] for p, g in json.load(f) if p.startswith(PREFIX)}
    return _files


def path(relative):
    return os.path.join(_files, relative)


def _guid(value):
    if isinstance(value, (bytes, bytearray)):
        return ''.join('%x%x' % (x & 15, x >> 4) for x in value)
    return str(value)


def read(file_path):
    import UnityPy
    env = UnityPy.load(file_path)
    obj = list(env.objects)[0]
    return obj, obj.read_typetree()


def ref_path(obj, ref):
    """Chemin du fichier visé par une référence {m_FileID, m_PathID} (None si absent)."""
    fid = ref.get('m_FileID', 0)
    if fid == 0:
        return None
    ext = obj.assets_file.externals[fid - 1]
    rel = _index.get(_guid(ext.guid))
    return path(rel) if rel else None


def quat_to_mat(q):
    x, y, z, w = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def trs(p, q, s):
    m = np.eye(4)
    m[:3, :3] = quat_to_mat(q) * np.asarray(s)[None, :]
    m[:3, 3] = p
    return m


def _v3(d):
    return np.array([d['x'], d['y'], d['z']], dtype=np.float64)


def _q4(d):
    return np.array([d['x'], d['y'], d['z'], d['w']], dtype=np.float64)


class Bone:
    def __init__(self, d):
        self.name = d['name']
        self.hash = d['hash']
        self.parent = d['parent']
        self.local = trs(_v3(d['position']), _q4(d['rotation']), _v3(d['scale']))


class Slot:
    def __init__(self, relative):
        self.relative = relative
        self.obj, t = read(path(relative))
        self.name = t.get('slotName') or t['m_Name']
        md = t['meshData']
        self.verts = np.array([[v['x'], v['y'], v['z']] for v in md['vertices']], dtype=np.float64)
        n = len(self.verts)
        self.normals = (np.array([[v['x'], v['y'], v['z']] for v in md['normals']], dtype=np.float64)
                        if md['normals'] else np.zeros((n, 3)))
        self.uv = (np.array([[v['x'], v['y']] for v in md['uv']], dtype=np.float64)
                   if md['uv'] else np.zeros((n, 2)))
        # Les triangles d'un sous-maillage UMA 3 empilent tous ses niveaux de détail
        # (lodRanges) : on ne garde que le plus fin, sinon les six se superposent.
        tris = []
        for s in md['submeshes']:
            t = np.array(s['triangles'], dtype=np.int64)
            ranges = s.get('lodRanges') or []
            if ranges:
                t = t[ranges[0]['offset']:ranges[0]['offset'] + ranges[0]['count']]
            tris.append(t.reshape(-1, 3))
        self.tris = np.concatenate(tris)
        self.bones = [Bone(b) for b in md['umaBones']]
        self.hashes = list(md['boneNameHashes'])
        self.bind = [np.array([[b['e%d%d' % (r, c)] for c in range(4)] for r in range(4)]) for b in md['bindPoses']]
        per = md.get('ManagedBonesPerVertex') or []
        mw = md.get('ManagedBoneWeights') or []
        self.weights = []
        if per and mw and len(per) == n:
            k = 0
            for c in per:
                self.weights.append([(w['m_BoneIndex'], w['m_Weight']) for w in mw[k:k + c]])
                k += c
        else:
            for w in md['boneWeights']:
                self.weights.append([(w['boneIndex%d' % i], w['weight%d' % i])
                                     for i in range(4) if w['weight%d' % i] > 0])


class Overlay:
    def __init__(self, relative):
        self.relative = relative
        full = path(relative)
        with open(full, 'rb') as f:
            head = f.read(5)
        if head == b'%YAML':
            self._read_yaml(full)
            return
        self.obj, t = read(full)
        self.name = t['m_Name']
        refs = t.get('_textureList') or t.get('textureList') or []
        self.textures = [ref_path(self.obj, r) for r in refs]
        r = t.get('rect') or {}
        self.rect = (r.get('x', 0.0), r.get('y', 0.0), r.get('width', 0.0), r.get('height', 0.0))

    def _read_yaml(self, full):
        """Les plus anciens overlays sont en YAML texte : on n'en lit que les textures et le rectangle."""
        import re
        text = open(full, encoding='utf-8', errors='replace').read()
        m = re.search(r'm_Name: (.*)', text)
        self.name = m.group(1).strip() if m else os.path.basename(full)
        block = re.search(r'_?textureList:\s*\n((?:\s*- \{[^}]*\}\s*\n)+)', text)
        self.textures = []
        if block:
            for g in re.findall(r'guid: ([0-9a-f]{32})', block.group(1)):
                rel = _index.get(g)
                self.textures.append(path(rel) if rel else None)
        r = re.search(r'rect:\s*\n\s*serializedVersion: \d+\s*\n\s*x: ([-\d.e]+)\s*\n\s*y: ([-\d.e]+)\s*\n\s*width: ([-\d.e]+)\s*\n\s*height: ([-\d.e]+)', text)
        self.rect = tuple(float(v) for v in r.groups()) if r else (0.0, 0.0, 0.0, 0.0)

    @property
    def albedo(self):
        return self.textures[0] if self.textures else None

    @property
    def normal(self):
        return self.textures[1] if len(self.textures) > 1 else None


class Skeleton:
    """Les os de plusieurs slots réunis par leur hash ; matrices monde de liaison."""

    def __init__(self, slots):
        self.bones = {}
        for s in slots:
            for b in s.bones:
                self.bones.setdefault(b.hash, b)
        self.names = {b.name: h for h, b in self.bones.items()}
        self.scales = {}
        self.world = {}
        self.rebuild()

    def rebuild(self):
        self.world = {}
        for h in self.bones:
            self._world(h)

    def _world(self, h):
        if h in self.world:
            return self.world[h]
        b = self.bones[h]
        local = b.local
        s = self.scales.get(b.name)
        if s is not None:
            local = local @ np.diag([s[0], s[1], s[2], 1.0])
        parent = self.bones.get(b.parent)
        m = local if parent is None else self._world(b.parent) @ local
        self.world[h] = m
        return m

    def scale(self, name, along=1.0, across=1.0, depth=None):
        """Met à l'échelle un os (UMA : X local le long de l'os, Y et Z en travers)."""
        if name in self.names:
            self.scales[name] = (along, across, across if depth is None else depth)

    def name_of(self, h):
        b = self.bones.get(h)
        return b.name if b else None

    def parent_name(self, name):
        b = self.bones.get(self.names.get(name))
        return self.name_of(b.parent) if b else None

    def position(self, name):
        h = self.names.get(name)
        return self.world[h][:3, 3].copy() if h is not None else None


def skin(slot, skeleton):
    """Positions et normales du slot posé sur le squelette (repère monde UMA)."""
    mats = []
    for i, h in enumerate(slot.hashes):
        w = skeleton.world.get(h)
        mats.append(w @ slot.bind[i] if w is not None else np.eye(4))
    mats = np.array(mats)
    n = len(slot.verts)
    acc = np.zeros((n, 4, 4))
    for v, pairs in enumerate(slot.weights):
        for bi, w in pairs:
            acc[v] += w * mats[bi]
    vh = np.c_[slot.verts, np.ones(n)]
    pos = np.einsum('nij,nj->ni', acc, vh)[:, :3]
    # Les normales suivent l'inverse transposée : un bras épaissi garde des normales justes.
    lin = acc[:, :3, :3]
    det = np.linalg.det(lin)
    lin[np.abs(det) < 1e-12] = np.eye(3)
    nor = np.einsum('nji,nj->ni', np.linalg.inv(lin), slot.normals)
    nor /= np.maximum(np.linalg.norm(nor, axis=1, keepdims=True), 1e-9)
    return pos, nor


def bone_names(slot, skeleton):
    """Nom de l'os UMA de chaque index d'os du slot."""
    return [skeleton.name_of(h) or '?' for h in slot.hashes]
