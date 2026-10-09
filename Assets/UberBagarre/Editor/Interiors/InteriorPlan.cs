using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Le plan d'un intérieur, avant d'être construit : des pièces (boîtes, cylindres, sphères)
    /// avec leur matériau, des lampes, des repères (l'arrivée, la porte, le comptoir, le
    /// vendeur) et des enseignes.
    ///
    /// Il ne dépend que des types de base d'Unity (vecteurs, couleurs) : le même plan est
    /// construit dans la scène par <c>InteriorRealizer</c> et rendu hors Unity (Blender) pour
    /// juger le résultat avant de livrer.
    ///
    /// Les meubles se décrivent dans leur propre repère : <see cref="Push"/> pose un repère
    /// (position, rotation) relatif au courant, <see cref="Pop"/> revient au précédent.
    /// </summary>
    public sealed class InteriorPlan
    {
        public enum Shape
        {
            Box,
            Cylinder,
            Sphere
        }

        public sealed class Surface
        {
            public string Key;
            public Color Color = Color.white;
            public float Smoothness = 0.3f;
            public float Metallic;
            public string Texture;
            public Color Emission = Color.black;
            public bool Glass;
        }

        public struct Part
        {
            public string Name;
            public Shape Shape;
            public Vector3 Position;
            public Quaternion Rotation;

            /// <summary>Boîte : dimensions ; cylindre : (diamètre, hauteur, diamètre) ; sphère : diamètres.</summary>
            public Vector3 Size;

            /// <summary>Le matériau ; null : une boîte invisible, seulement pour la collision.</summary>
            public string Surface;

            public bool Collider;
            public string Group;
        }

        public struct Lamp
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Color Color;
            public float Intensity;
            public float Range;
            public bool Spot;
            public float Angle;
            public bool Shadows;
        }

        public struct Marker
        {
            public string Key;
            public Vector3 Position;
            public Quaternion Rotation;

            /// <summary>La longueur de ce qu'il repère (le comptoir : la zone d'achat).</summary>
            public float Length;
        }

        public struct Sign
        {
            public string Text;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Height;
            public string Surface;
        }

        /// <summary>Les dimensions intérieures de la pièce (largeur x, hauteur y, profondeur z).</summary>
        public Vector3 Size;

        public readonly Dictionary<string, Surface> Surfaces = new Dictionary<string, Surface>();
        public readonly List<Part> Parts = new List<Part>(1024);
        public readonly List<Lamp> Lamps = new List<Lamp>();
        public readonly List<Marker> Markers = new List<Marker>();
        public readonly List<Sign> Signs = new List<Sign>();

        /// <summary>Le groupe des pièces posées maintenant (un meuble utile : « comptoir », « porte »…).</summary>
        public string Group = "";

        private Vector3 _origin = Vector3.zero;
        private Quaternion _rotation = Quaternion.identity;
        private readonly Stack<Vector3> _origins = new Stack<Vector3>();
        private readonly Stack<Quaternion> _rotations = new Stack<Quaternion>();
        private readonly Stack<string> _groups = new Stack<string>();

        // ------------------------------------------------------------------ repères

        public void Push(Vector3 position, float yaw)
        {
            _origins.Push(_origin);
            _rotations.Push(_rotation);
            _groups.Push(Group);
            _origin = _origin + _rotation * position;
            _rotation = _rotation * Quaternion.Euler(0f, yaw, 0f);
        }

        public void Push(Vector3 position, Vector3 euler)
        {
            _origins.Push(_origin);
            _rotations.Push(_rotation);
            _groups.Push(Group);
            _origin = _origin + _rotation * position;
            _rotation = _rotation * Quaternion.Euler(euler);
        }

        public void Pop()
        {
            _origin = _origins.Pop();
            _rotation = _rotations.Pop();
            Group = _groups.Pop();
        }

        /// <summary>Un point du repère courant, en coordonnées de la pièce.</summary>
        public Vector3 Point(Vector3 local)
        {
            return _origin + _rotation * local;
        }

        public Quaternion Rotation(Vector3 euler)
        {
            return _rotation * Quaternion.Euler(euler);
        }

        // ------------------------------------------------------------------ matériaux

        public Surface Define(string key, Color color, float smoothness, float metallic, string texture = null)
        {
            Surface s;
            if (!Surfaces.TryGetValue(key, out s))
            {
                s = new Surface { Key = key };
                Surfaces[key] = s;
            }

            s.Color = color;
            s.Smoothness = smoothness;
            s.Metallic = metallic;
            s.Texture = texture;
            return s;
        }

        public Surface Glow(string key, Color color, Color emission)
        {
            Surface s = Define(key, color, 0.6f, 0f);
            s.Emission = emission;
            return s;
        }

        public Surface Glass(string key, Color tint, float smoothness = 0.95f)
        {
            Surface s = Define(key, tint, smoothness, 0f);
            s.Glass = true;
            return s;
        }

        // ------------------------------------------------------------------ pièces

        public void Box(string name, Vector3 center, Vector3 size, string surface, bool collider = false)
        {
            Add(name, Shape.Box, center, Vector3.zero, size, surface, collider);
        }

        public void Box(string name, Vector3 center, Vector3 size, string surface, Vector3 euler, bool collider = false)
        {
            Add(name, Shape.Box, center, euler, size, surface, collider);
        }

        /// <summary>Une boîte qui ne se voit pas : seulement pour arrêter le joueur (un meuble fait de cent petites pièces).</summary>
        public void Block(string name, Vector3 center, Vector3 size, float yaw = 0f)
        {
            Add(name, Shape.Box, center, new Vector3(0f, yaw, 0f), size, null, true);
        }

        /// <summary>Un cylindre à base ovale (<paramref name="size"/> : diamètres x et z, hauteur y).</summary>
        public void Oval(string name, Vector3 center, Vector3 size, string surface, Vector3 euler = default(Vector3), bool collider = false)
        {
            Add(name, Shape.Cylinder, center, euler, size, surface, collider);
        }

        /// <summary>Un cylindre debout (axe Y local), sauf rotation <paramref name="euler"/>.</summary>
        public void Cylinder(string name, Vector3 center, float radius, float height, string surface, Vector3 euler = default(Vector3),
            bool collider = false)
        {
            Add(name, Shape.Cylinder, center, euler, new Vector3(radius * 2f, height, radius * 2f), surface, collider);
        }

        public void Sphere(string name, Vector3 center, Vector3 diameters, string surface, Vector3 euler = default(Vector3))
        {
            Add(name, Shape.Sphere, center, euler, diameters, surface, false);
        }

        private void Add(string name, Shape shape, Vector3 center, Vector3 euler, Vector3 size, string surface, bool collider)
        {
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) return;
            Parts.Add(new Part
            {
                Name = name,
                Shape = shape,
                Position = Point(center),
                Rotation = Rotation(euler),
                Size = size,
                Surface = surface,
                Collider = collider,
                Group = Group
            });
        }

        // ------------------------------------------------------------------ lumière, repères, enseignes

        public void Light(Vector3 position, Color color, float intensity, float range, bool shadows = false)
        {
            Lamps.Add(new Lamp
            {
                Position = Point(position),
                Rotation = _rotation,
                Color = color,
                Intensity = intensity,
                Range = range,
                Shadows = shadows
            });
        }

        /// <summary>Un projecteur dirigé selon <paramref name="euler"/> (90° en X : vers le bas).</summary>
        public void Spot(Vector3 position, Vector3 euler, Color color, float intensity, float range, float angle, bool shadows = false)
        {
            Lamps.Add(new Lamp
            {
                Position = Point(position),
                Rotation = Rotation(euler),
                Color = color,
                Intensity = intensity,
                Range = range,
                Spot = true,
                Angle = angle,
                Shadows = shadows
            });
        }

        public void Mark(string key, Vector3 position, float yaw, float length = 0f)
        {
            Markers.Add(new Marker { Key = key, Position = Point(position), Rotation = Rotation(new Vector3(0f, yaw, 0f)), Length = length });
        }

        public bool TryMarker(string key, out Marker marker)
        {
            for (int i = 0; i < Markers.Count; i++)
            {
                if (Markers[i].Key != key) continue;
                marker = Markers[i];
                return true;
            }

            marker = default(Marker);
            return false;
        }

        /// <summary>Une enseigne au néon, lisible depuis le −Z du repère.</summary>
        public void Neon(string text, Vector3 position, float yaw, float height, string surface)
        {
            Signs.Add(new Sign { Text = text, Position = Point(position), Rotation = Rotation(new Vector3(0f, yaw, 0f)), Height = height, Surface = surface });
        }
    }
}
