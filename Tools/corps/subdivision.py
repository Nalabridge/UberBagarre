"""
Subdivision de Catmull-Clark (un niveau), en numpy, sans dépendance.

Le maillage MakeHuman a des doigts de huit côtés : à un mètre de la caméra, un poing se
lit en facettes. Un niveau de subdivision quadruple les faces et arrondit les phalanges,
les jointures et la pulpe du pouce. Les poids de peau, les coordonnées de texture et les
étiquettes de face suivent : poids interpolés comme les positions, UV interpolées
linéairement dans chaque face (les coutures de texture restent exactement en place).

Bords ouverts (ourlets, manches, yeux) : règle de bord classique (arête = milieu, sommet =
(voisin + 6 x sommet + voisin) / 8), pour que le bord ne se rétracte pas en dents de scie.
"""
import numpy as np


def subdivide(positions, faces, face_uvs, attributes):
    """
    positions  : (n, 3)
    faces      : liste de tuples d'indices (3 ou 4 sommets, ou plus)
    face_uvs   : liste de listes d'UV par coin (même forme que faces)
    attributes : liste de tableaux (n, k) interpolés comme les positions (poids de peau...)
    Renvoie (positions, faces, face_uvs, attributes, parent) — parent[i] = face d'origine.
    """
    positions = np.asarray(positions, dtype=np.float64)
    n = len(positions)

    sizes = np.array([len(f) for f in faces])
    starts = np.concatenate([[0], np.cumsum(sizes)[:-1]])
    corner_v = np.concatenate([np.asarray(f, dtype=np.int64) for f in faces])
    corner_face = np.repeat(np.arange(len(faces)), sizes)
    corner_next = np.arange(len(corner_v)) + 1
    last = starts + sizes - 1
    corner_next[last] = starts
    corner_prev = np.arange(len(corner_v)) - 1
    corner_prev[starts] = last

    a = corner_v
    b = corner_v[corner_next]
    lo = np.minimum(a, b)
    hi = np.maximum(a, b)
    keys = lo * (n + 1) + hi
    unique_keys, corner_edge = np.unique(keys, return_inverse=True)
    edge_count = len(unique_keys)
    edge_a = unique_keys // (n + 1)
    edge_b = unique_keys % (n + 1)
    edge_faces = np.bincount(corner_edge, minlength=edge_count)
    boundary_edge = edge_faces == 1

    def face_mean(values):
        acc = np.zeros((len(faces),) + values.shape[1:])
        np.add.at(acc, corner_face, values[corner_v])
        return acc / sizes.reshape((-1,) + (1,) * (values.ndim - 1))

    face_point = face_mean(positions)

    # Points d'arête.
    edge_face_sum = np.zeros((edge_count, 3))
    np.add.at(edge_face_sum, corner_edge, face_point[corner_face])
    mid = (positions[edge_a] + positions[edge_b]) * 0.5
    edge_point = mid.copy()
    interior = ~boundary_edge & (edge_faces == 2)
    edge_point[interior] = (positions[edge_a[interior]] + positions[edge_b[interior]] +
                            edge_face_sum[interior]) / 4.0

    # Points de sommet.
    valence = np.bincount(np.concatenate([edge_a, edge_b]), minlength=n).astype(np.float64)
    mid_sum = np.zeros((n, 3))
    np.add.at(mid_sum, edge_a, mid)
    np.add.at(mid_sum, edge_b, mid)
    face_sum = np.zeros((n, 3))
    faces_per_vertex = np.bincount(corner_v, minlength=n).astype(np.float64)
    np.add.at(face_sum, corner_v, face_point[corner_face])

    vertex_point = positions.copy()
    ok = (valence >= 3) & (faces_per_vertex > 0)
    safe_val = np.where(valence > 0, valence, 1)
    safe_faces = np.where(faces_per_vertex > 0, faces_per_vertex, 1)
    f_avg = face_sum / safe_faces[:, None]
    r_avg = mid_sum / safe_val[:, None]
    interior_v = ok.copy()

    boundary_count = np.bincount(np.concatenate([edge_a[boundary_edge], edge_b[boundary_edge]]), minlength=n)
    interior_v &= boundary_count == 0
    vn = valence[:, None]
    vertex_point[interior_v] = ((f_avg + 2 * r_avg + (vn - 3) * positions) / vn)[interior_v]

    # Sommets de bord : (voisin + 6 v + voisin) / 8, s'il a exactement deux arêtes de bord.
    nb_sum = np.zeros((n, 3))
    ea, eb = edge_a[boundary_edge], edge_b[boundary_edge]
    np.add.at(nb_sum, ea, positions[eb])
    np.add.at(nb_sum, eb, positions[ea])
    on_boundary = boundary_count == 2
    vertex_point[on_boundary] = ((nb_sum + 6 * positions) / 8.0)[on_boundary]

    # Nouveaux indices : [sommets | arêtes | faces].
    new_positions = np.concatenate([vertex_point, edge_point, face_point])
    e_off = n
    f_off = n + edge_count

    # Attributs : interpolation linéaire (sommet gardé, milieu d'arête, moyenne de face).
    new_attributes = []
    for attr in attributes:
        attr = np.asarray(attr, dtype=np.float64)
        e_attr = (attr[edge_a] + attr[edge_b]) * 0.5
        f_attr = face_mean(attr)
        new_attributes.append(np.concatenate([attr, e_attr, f_attr]))

    # Nouvelles faces : un quad par coin (sommet, arête suivante, centre, arête précédente).
    corner_uv = np.concatenate([np.asarray(u, dtype=np.float64) for u in face_uvs])
    face_uv_center = np.zeros((len(faces), 2))
    np.add.at(face_uv_center, corner_face, corner_uv)
    face_uv_center /= sizes[:, None]

    e_next = corner_edge
    e_prev = corner_edge[corner_prev]
    quads = np.stack([corner_v, e_off + e_next, f_off + corner_face, e_off + e_prev], axis=1)
    uv_next = (corner_uv + corner_uv[corner_next]) * 0.5
    uv_prev = (corner_uv + corner_uv[corner_prev]) * 0.5
    quad_uv = np.stack([corner_uv, uv_next, face_uv_center[corner_face], uv_prev], axis=1)

    return new_positions, quads, quad_uv, new_attributes, corner_face
