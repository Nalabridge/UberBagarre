using System;
using UnityEngine;

namespace UberBagarre.View
{
    public enum HandSide
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// Position et rotation d'un poing, exprimées en espace caméra
    /// (X = droite, Y = haut, Z = avant, origine = l'œil du joueur).
    ///
    /// C'est l'unité de base de toute l'animation des mains : la garde, la respiration,
    /// et plus tard chaque image-clé d'attaque sont des HandPose. Un seul type à
    /// comprendre, et les attaques deviennent des données interpolables.
    /// </summary>
    [Serializable]
    public struct HandPose
    {
        [Tooltip("Position du poing en espace camera, en metres.")]
        public Vector3 position;

        [Tooltip("Rotation du poing en espace camera, en degres.")]
        public Vector3 euler;

        public HandPose(Vector3 position, Vector3 euler)
        {
            this.position = position;
            this.euler = euler;
        }

        public static HandPose Lerp(HandPose a, HandPose b, float t)
        {
            return new HandPose(
                Vector3.Lerp(a.position, b.position, t),
                new Vector3(
                    Mathf.LerpAngle(a.euler.x, b.euler.x, t),
                    Mathf.LerpAngle(a.euler.y, b.euler.y, t),
                    Mathf.LerpAngle(a.euler.z, b.euler.z, t)));
        }

        public static HandPose operator +(HandPose a, HandPose b)
        {
            return new HandPose(a.position + b.position, a.euler + b.euler);
        }

        public Quaternion Rotation
        {
            get { return Quaternion.Euler(euler); }
        }
    }
}
